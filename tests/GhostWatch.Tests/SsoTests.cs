using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Eve.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace GhostWatch.Tests;

public class SsoTests
{
    private static TestApplication App(FakeSso upstream) => new(builder => builder.ConfigureTestServices(services =>
    {
        services.PostConfigure<EveOptions>(options =>
        {
            options.ClientId = "test-client";
            options.ClientSecret = "test-secret";
            options.CallbackUrl = "http://localhost/api/auth/eve/callback";
        });
        services.AddHttpClient<SsoClient>().ConfigurePrimaryHttpMessageHandler(() => upstream);
    }));

    private static HttpClient Browser(TestApplication app) => app.CreateClient(new WebApplicationFactoryClientOptions
        { AllowAutoRedirect = false, HandleCookies = false });

    private static async Task<(string State, string Cookie, string Challenge)> Start(HttpClient browser)
    {
        var response = await browser.GetAsync("/api/auth/eve/start");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var target = response.Headers.Location!;
        Assert.Equal("login.eveonline.com", target.Host);
        var query = QueryHelpers.ParseQuery(target.Query);
        Assert.Equal("S256", query["code_challenge_method"].ToString());
        Assert.DoesNotContain("code_verifier", target.ToString());
        var cookie = response.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("samesite=lax", cookie.ToLowerInvariant());
        Assert.True(response.Headers.CacheControl!.NoStore);
        return (query["state"].ToString(), cookie.Split(';')[0], query["code_challenge"].ToString());
    }

    private static async Task<HttpResponseMessage> Callback(HttpClient browser, string state, string? cookie, string query = "code=test-code")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/auth/eve/callback?state={state}&{query}");
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        return await browser.SendAsync(request);
    }

    [Fact]
    public async Task Pkce_login_encrypts_tokens_reconnects_without_duplicates_and_supports_multiple_characters()
    {
        using var upstream = new FakeSso();
        await using var app = App(upstream);
        using var browser = Browser(app);
        var login = await Start(browser);
        var callback = await Callback(browser, login.State, login.Cookie);
        Assert.Equal("/characters?auth=connected", callback.Headers.Location!.ToString());
        Assert.Equal(login.Challenge, WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(upstream.LastForm["code_verifier"].ToString()))));
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("test-client:test-secret")), upstream.Authorization);
        Assert.False(upstream.LastForm.ContainsKey("client_secret"));
        Assert.False(upstream.LastForm.ContainsKey("client_id"));
        using (var scope = app.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>().EveCharacters.SingleAsync();
            Assert.NotEqual("refresh-test-token", row.RefreshToken);
            Assert.Equal("refresh-test-token", scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("GhostWatch.Eve.RefreshToken.v1").Unprotect(row.RefreshToken));
        }
        upstream.CharacterName = "Renamed character";
        login = await Start(browser);
        Assert.Equal("/characters?auth=connected", (await Callback(browser, login.State, login.Cookie)).Headers.Location!.ToString());
        var list = await browser.GetStringAsync("/api/eve/characters");
        Assert.DoesNotContain("refresh", list, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("owner", list, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(upstream.AccessToken, list);
        Assert.Single(JsonNode.Parse(list)!.AsArray());
        Assert.Contains("Renamed character", list);
        upstream.CharacterId = 90000002;
        login = await Start(browser);
        await Callback(browser, login.State, login.Cookie);
        Assert.Equal(2, JsonNode.Parse(await browser.GetStringAsync("/api/eve/characters"))!.AsArray().Count);
    }

    [Fact]
    public async Task Wrong_browser_and_replayed_callback_never_exchange_tokens()
    {
        using var upstream = new FakeSso();
        await using var app = App(upstream);
        using var browser = Browser(app);
        var login = await Start(browser);
        Assert.Equal("/characters?auth=invalid-state", (await Callback(browser, login.State, null)).Headers.Location!.ToString());
        Assert.Equal("/characters?auth=invalid-state", (await Callback(browser, login.State, "ghost-watch-oauth=wrong")).Headers.Location!.ToString());
        Assert.Equal(0, upstream.Exchanges);
        Assert.Equal("/characters?auth=connected", (await Callback(browser, login.State, login.Cookie)).Headers.Location!.ToString());
        Assert.Equal("/characters?auth=invalid-state", (await Callback(browser, login.State, login.Cookie)).Headers.Location!.ToString());
        Assert.Equal(1, upstream.Exchanges);
    }

    [Theory]
    [InlineData("audience")]
    [InlineData("issuer")]
    [InlineData("expired")]
    [InlineData("signature")]
    public async Task Invalid_jwt_does_not_persist_a_character(string failure)
    {
        using var upstream = new FakeSso { InvalidToken = failure };
        await using var app = App(upstream);
        using var browser = Browser(app);
        var login = await Start(browser);
        Assert.Equal("/characters?auth=invalid-token", (await Callback(browser, login.State, login.Cookie)).Headers.Location!.ToString());
        Assert.Equal("[]", await browser.GetStringAsync("/api/eve/characters"));
    }

    [Fact]
    public async Task Cancellation_consumes_state_without_exchange_and_errors_do_not_echo_provider_text()
    {
        using var upstream = new FakeSso();
        await using var app = App(upstream);
        using var browser = Browser(app);
        var login = await Start(browser);
        Assert.Equal("/characters?auth=cancelled", (await Callback(browser, login.State, login.Cookie, "error=access_denied")).Headers.Location!.ToString());
        Assert.Equal(0, upstream.Exchanges);
        Assert.Equal("/characters?auth=invalid-state", (await Callback(browser, login.State, login.Cookie)).Headers.Location!.ToString());
        upstream.FailExchange = true;
        login = await Start(browser);
        var failed = await Callback(browser, login.State, login.Cookie);
        Assert.Equal("/characters?auth=invalid-grant", failed.Headers.Location!.ToString());
        Assert.DoesNotContain("private-provider-detail", await failed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Refresh_rotates_protected_token_and_reuses_cached_access_token()
    {
        using var upstream = new FakeSso();
        await using var app = App(upstream);
        using var browser = Browser(app);
        var login = await Start(browser);
        await Callback(browser, login.State, login.Cookie);
        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CharacterAccessTokens>();
        var token = await service.Get(upstream.CharacterId, default);
        Assert.Equal(upstream.AccessToken, token);
        Assert.Equal("refresh_token", upstream.LastForm["grant_type"].ToString());
        Assert.Equal("refresh-test-token", upstream.LastForm["refresh_token"].ToString());
        var row = await scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>().EveCharacters.SingleAsync();
        Assert.Equal("rotated-refresh-token", scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("GhostWatch.Eve.RefreshToken.v1").Unprotect(row.RefreshToken));
        Assert.Equal(token, await service.Get(upstream.CharacterId, default));
        Assert.Equal(2, upstream.Exchanges);
    }

    [Fact]
    public async Task Owner_change_cannot_rebind_character_or_overwrite_local_tracks()
    {
        using var upstream = new FakeSso();
        await using var app = App(upstream);
        using var browser = Browser(app);
        var created = await browser.PostAsJsonAsync("/api/economics/tracks", new
            { name = "Local programme", description = "", notes = "Keep this decision", status = "Active", purpose = "Other" });
        created.EnsureSuccessStatusCode();
        var login = await Start(browser);
        await Callback(browser, login.State, login.Cookie);
        upstream.Owner = "different-owner";
        login = await Start(browser);
        Assert.Equal("/characters?auth=ownership-changed", (await Callback(browser, login.State, login.Cookie)).Headers.Location!.ToString());
        Assert.Contains("Keep this decision", await browser.GetStringAsync(created.Headers.Location));
        using var scope = app.Services.CreateScope();
        await Assert.ThrowsAsync<SsoException>(() => scope.ServiceProvider.GetRequiredService<CharacterAccessTokens>().Get(upstream.CharacterId, default));
    }

    [Fact]
    public async Task Missing_configuration_is_actionable_and_never_exposes_a_secret()
    {
        await using var app = new TestApplication();
        using var browser = Browser(app);
        Assert.Equal("/characters?auth=configuration", (await browser.GetAsync("/api/auth/eve/start")).Headers.Location!.ToString());
        var config = await browser.GetStringAsync("/api/auth/eve/config");
        Assert.Contains("\"configured\":false", config);
        Assert.DoesNotContain("clientSecret", config);
    }

    [Theory]
    [InlineData("http://login.eveonline.com/token")]
    [InlineData("https://attacker.invalid/token")]
    [InlineData("https://login.eveonline.com:8443/token")]
    public void Metadata_cannot_redirect_credentials_to_an_untrusted_endpoint(string endpoint)
        => Assert.Throws<InvalidOperationException>(() => SsoClient.TrustedEndpoint(new JsonObject { ["token_endpoint"] = endpoint }, "token_endpoint"));

    [Fact]
    public async Task Concurrent_callbacks_can_consume_pending_login_only_once()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var logins = new PendingLogins(cache);
        logins.Add("state", new("verifier", "browser"));
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => logins.Consume("state", "browser"))));
        Assert.Single(results, x => x is not null);
    }

    [Fact]
    public async Task Undecryptable_token_requires_reconnection_without_contacting_Eve()
    {
        using var upstream = new FakeSso();
        await using var app = App(upstream);
        using var browser = Browser(app);
        var login = await Start(browser);
        await Callback(browser, login.State, login.Cookie);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
        var character = await db.EveCharacters.SingleAsync();
        character.RefreshToken = "unreadable-token";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<SsoException>(() => scope.ServiceProvider.GetRequiredService<CharacterAccessTokens>().Get(upstream.CharacterId, default));
        Assert.Equal(1, upstream.Exchanges);
    }

    private sealed class FakeSso : HttpMessageHandler
    {
        private readonly RSA rsa = RSA.Create(2048);
        public long CharacterId { get; set; } = 90000001;
        public string CharacterName { get; set; } = "Test character";
        public string Owner { get; set; } = "test-owner";
        public string? InvalidToken { get; set; }
        public bool FailExchange { get; set; }
        public int Exchanges { get; private set; }
        public string AccessToken { get; private set; } = "";
        public string Authorization { get; private set; } = "";
        public Dictionary<string, Microsoft.Extensions.Primitives.StringValues> LastForm { get; private set; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("oauth-authorization-server")) return Json(new
            {
                authorization_endpoint = "https://login.eveonline.com/v2/oauth/authorize",
                token_endpoint = "https://login.eveonline.com/v2/oauth/token",
                jwks_uri = "https://login.eveonline.com/oauth/jwks"
            });
            if (path.EndsWith("jwks"))
            {
                var key = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = "test-key" });
                return Json(new { keys = new[] { key } });
            }
            if (!path.EndsWith("token")) throw new InvalidOperationException("Unexpected test endpoint.");
            Exchanges++;
            Authorization = request.Headers.Authorization!.ToString();
            LastForm = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(ct));
            if (FailExchange) return new HttpResponseMessage(HttpStatusCode.BadRequest)
                { Content = JsonContent.Create(new { error = "invalid_grant", error_description = "private-provider-detail" }) };
            using var other = RSA.Create(2048);
            var keyToSign = new RsaSecurityKey(InvalidToken == "signature" ? other : rsa) { KeyId = "test-key" };
            var claims = new[] { new Claim("sub", $"CHARACTER:EVE:{CharacterId}"), new Claim("name", CharacterName),
                new Claim("owner", Owner), new Claim("aud", InvalidToken == "audience" ? "wrong-client" : "test-client") };
            AccessToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                InvalidToken == "issuer" ? "https://attacker.invalid" : "https://login.eveonline.com", "EVE Online", claims,
                DateTime.UtcNow.AddHours(-2), InvalidToken == "expired" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(20),
                new SigningCredentials(keyToSign, SecurityAlgorithms.RsaSha256)));
            return Json(new { access_token = AccessToken, refresh_token = LastForm["grant_type"] == "refresh_token" ? "rotated-refresh-token" : "refresh-test-token" });
        }
        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
        protected override void Dispose(bool disposing) { if (disposing) rsa.Dispose(); base.Dispose(disposing); }
    }
}
