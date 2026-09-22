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

    private static async Task<(string State, string Cookie, string Challenge)> Start(HttpClient browser, long? characterId = null)
    {
        var response = await browser.GetAsync("/api/auth/eve/start" + (characterId is null ? "" : $"?characterId={characterId}"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var target = response.Headers.Location!;
        Assert.Equal("login.eveonline.com", target.Host);
        var query = QueryHelpers.ParseQuery(target.Query);
        Assert.Equal(EveScopes.Required, query["scope"].ToString().Split(' '));
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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task List_and_detail_permissions_come_from_actual_signed_grants(int missing)
    {
        using var upstream = new FakeSso { GrantedScopes = EveScopes.Required.Skip(missing).ToArray() };
        await using var app = App(upstream);
        using var browser = Browser(app);
        var login = await Start(browser);
        await Callback(browser, login.State, login.Cookie);
        var list = (await browser.GetFromJsonAsync<JsonArray>("/api/eve/characters"))!;
        var detail = (await browser.GetFromJsonAsync<JsonObject>($"/api/eve/characters/{upstream.CharacterId}/data"))!;
        var permissions = list[0]!["permissions"]!;
        Assert.Equal(missing == 0, permissions["hasAllRequiredScopes"]!.GetValue<bool>());
        Assert.Equal(missing, permissions["missingScopeCount"]!.GetValue<int>());
        Assert.Equal(EveScopes.Required.Take(missing), permissions["missingScopes"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(permissions.ToJsonString(), detail["permissions"]!.ToJsonString());
        Assert.DoesNotContain("grantedScopesJson", list.ToJsonString());
    }

    [Fact]
    public void Future_required_scopes_and_unverified_legacy_grants_cannot_appear_healthy()
    {
        var grants = EveScopes.Store(EveScopes.Required);
        Assert.True(EveScopes.Permissions(grants).HasAllRequiredScopes);
        var changed = EveScopes.Compare(grants, EveScopes.Required.Append("future-permission"));
        Assert.Equal(new[] { "future-permission" }, changed.MissingScopes);
        Assert.False(changed.HasAllRequiredScopes);
        var legacy = EveScopes.Permissions(null);
        Assert.False(legacy.ScopesKnown);
        Assert.False(legacy.HasAllRequiredScopes);
    }

    [Fact]
    public async Task Targeted_reauthorisation_updates_grants_in_place_and_preserves_existing_data()
    {
        using var upstream = new FakeSso { GrantedScopes = EveScopes.Required.Skip(2).ToArray() };
        await using var app = App(upstream);
        using var browser = Browser(app);
        var login = await Start(browser);
        await Callback(browser, login.State, login.Cookie);
        DateTime connected;
        string encrypted;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
            var character = await db.EveCharacters.SingleAsync();
            connected = character.ConnectedAt;
            encrypted = character.RefreshToken;
            db.EveSections.Add(new() { CharacterId = character.CharacterId, Name = "wallet", Json = "123.45" });
            db.EveLocationNames.Add(new() { CharacterId = character.CharacterId, LocationId = 1000000000001, Name = "Linked location", ExpiresAt = DateTime.UtcNow.AddHours(1) });
            var track = new GhostWatch.Api.Economics.Tracks.EconomyTrack { Name = "Programme", Notes = "Keep this local plan" };
            db.EconomyTracks.Add(track);
            var account = new GhostWatch.Api.Management.ManagedAccount { Name = "Main", Subscription = "Omega" };
            db.ManagedAccounts.Add(account);
            db.CharacterPlans.Add(new() { CharacterId = character.CharacterId, AccountId = account.Id, Assignment = "Controller" });
            db.CharacterTracks.Add(new() { CharacterId = character.CharacterId, TrackId = track.Id });
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<CharacterAccessTokens>().Get(character.CharacterId, default);
        }
        upstream.GrantedScopes = EveScopes.Required.ToArray();
        login = await Start(browser, upstream.CharacterId);
        Assert.Equal($"/characters/{upstream.CharacterId}?auth=reauthorised", (await Callback(browser, login.State, login.Cookie)).Headers.Location!.ToString());
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
            var character = await db.EveCharacters.SingleAsync();
            Assert.Equal(connected, character.ConnectedAt);
            Assert.NotEqual(encrypted, character.RefreshToken);
            Assert.True(EveScopes.Permissions(character.GrantedScopesJson).HasAllRequiredScopes);
            Assert.Equal("123.45", (await db.EveSections.SingleAsync()).Json);
            Assert.Equal("Linked location", (await db.EveLocationNames.SingleAsync()).Name);
            Assert.Equal("Keep this local plan", (await db.EconomyTracks.SingleAsync()).Notes);
            Assert.Equal("Omega", (await db.ManagedAccounts.SingleAsync()).Subscription);
            Assert.Equal("Controller", (await db.CharacterPlans.SingleAsync()).Assignment);
            Assert.Single(await db.CharacterTracks.ToListAsync());
            var exchanges = upstream.Exchanges;
            await scope.ServiceProvider.GetRequiredService<CharacterAccessTokens>().Get(character.CharacterId, default);
            Assert.Equal(exchanges + 1, upstream.Exchanges); // Re-authorisation invalidates old access-token cache.
        }
    }

    [Theory]
    [InlineData("wrong-character")]
    [InlineData("cancelled")]
    [InlineData("invalid-grant")]
    [InlineData("invalid-token")]
    public async Task Unsuccessful_targeted_reauthorisation_preserves_credentials_and_grants(string failure)
    {
        using var upstream = new FakeSso { GrantedScopes = EveScopes.Required.Skip(1).ToArray() };
        await using var app = App(upstream);
        using var browser = Browser(app);
        var login = await Start(browser);
        await Callback(browser, login.State, login.Cookie);
        var id = upstream.CharacterId;
        string refresh; string? grants; DateTime authenticated;
        using (var scope = app.Services.CreateScope())
        {
            var character = await scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>().EveCharacters.SingleAsync();
            refresh = character.RefreshToken; grants = character.GrantedScopesJson; authenticated = character.LastAuthenticatedAt;
        }
        login = await Start(browser, id);
        upstream.GrantedScopes = EveScopes.Required.ToArray();
        if (failure == "wrong-character") upstream.CharacterId++;
        if (failure == "invalid-grant") upstream.FailExchange = true;
        if (failure == "invalid-token") upstream.InvalidToken = "signature";
        var response = await Callback(browser, login.State, login.Cookie, failure == "cancelled" ? "error=access_denied" : "code=test-code");
        Assert.Equal($"/characters/{id}?auth={failure}", response.Headers.Location!.ToString());
        using var check = app.Services.CreateScope();
        var saved = await check.ServiceProvider.GetRequiredService<GhostWatchDbContext>().EveCharacters.SingleAsync();
        Assert.Equal(id, saved.CharacterId);
        Assert.Equal(refresh, saved.RefreshToken);
        Assert.Equal(grants, saved.GrantedScopesJson);
        Assert.Equal(authenticated, saved.LastAuthenticatedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Normal_refresh_updates_actual_grants_even_without_refresh_token_rotation(bool omitRotation)
    {
        using var upstream = new FakeSso();
        await using var app = App(upstream);
        using var browser = Browser(app);
        var login = await Start(browser);
        await Callback(browser, login.State, login.Cookie);
        upstream.GrantedScopes = EveScopes.Required.Skip(1).ToArray();
        upstream.OmitRotatedToken = omitRotation;
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CharacterAccessTokens>().Get(upstream.CharacterId, default);
        var character = await scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>().EveCharacters.SingleAsync();
        Assert.Equal(1, EveScopes.Permissions(character.GrantedScopesJson).MissingScopeCount);
        Assert.Equal(omitRotation ? "refresh-test-token" : "rotated-refresh-token", scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("GhostWatch.Eve.RefreshToken.v1").Unprotect(character.RefreshToken));
    }

    [Fact]
    public async Task Missing_scope_claim_is_never_assumed_to_grant_requested_permissions()
    {
        using var upstream = new FakeSso { GrantedScopes = null };
        await using var app = App(upstream);
        using var browser = Browser(app);
        var login = await Start(browser);
        await Callback(browser, login.State, login.Cookie);
        using var scope = app.Services.CreateScope();
        var character = await scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>().EveCharacters.SingleAsync();
        Assert.Equal("[]", character.GrantedScopesJson);
        Assert.False(EveScopes.Permissions(character.GrantedScopesJson).HasAllRequiredScopes);
    }

    private sealed class FakeSso : HttpMessageHandler
    {
        private readonly RSA rsa = RSA.Create(2048);
        public long CharacterId { get; set; } = 90000001;
        public string CharacterName { get; set; } = "Test character";
        public string Owner { get; set; } = "test-owner";
        public string? InvalidToken { get; set; }
        public string[]? GrantedScopes { get; set; } = EveScopes.Required.ToArray();
        public bool OmitRotatedToken { get; set; }
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
            var jwt = new JwtSecurityToken(
                InvalidToken == "issuer" ? "https://attacker.invalid" : "https://login.eveonline.com", "EVE Online", claims,
                DateTime.UtcNow.AddHours(-2), InvalidToken == "expired" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(20),
                new SigningCredentials(keyToSign, SecurityAlgorithms.RsaSha256));
            if (GrantedScopes is not null) jwt.Payload["scp"] = GrantedScopes;
            AccessToken = new JwtSecurityTokenHandler().WriteToken(jwt);
            if (OmitRotatedToken && LastForm["grant_type"] == "refresh_token") return Json(new { access_token = AccessToken });
            return Json(new { access_token = AccessToken, refresh_token = LastForm["grant_type"] == "refresh_token" ? "rotated-refresh-token" : "refresh-test-token" });
        }
        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
        protected override void Dispose(bool disposing) { if (disposing) rsa.Dispose(); base.Dispose(disposing); }
    }
}
