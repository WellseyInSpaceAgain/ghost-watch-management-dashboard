using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Eve;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GhostWatch.Api.Eve.Auth;

public class SsoClient(HttpClient http, IMemoryCache cache, IOptions<EveOptions> options,
    IDataProtectionProvider protection, GhostWatchDbContext db)
{
    private readonly EveOptions config = options.Value;
    private readonly IDataProtector protector = protection.CreateProtector("GhostWatch.Eve.RefreshToken.v1");

    public async Task<JsonObject> Metadata(CancellationToken ct) =>
        (await cache.GetOrCreateAsync("sso-metadata", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return (await http.GetFromJsonAsync<JsonObject>("https://login.eveonline.com/.well-known/oauth-authorization-server", ct))!;
        }))!;

    public static string TrustedEndpoint(JsonObject metadata, string key)
    {
        var uri = new Uri(metadata[key]!.GetValue<string>());
        if (uri.Scheme != "https" || uri.Host != "login.eveonline.com" || !uri.IsDefaultPort || uri.UserInfo != "") throw new InvalidOperationException("Invalid SSO endpoint.");
        return uri.AbsoluteUri;
    }

    public async Task<JsonObject> Exchange(Dictionary<string, string> fields, CancellationToken ct)
    {
        // CCP rejects client credentials in both the Basic header and the form body.
        // This confidential client authenticates through Basic, including when using PKCE.
        using var request = new HttpRequestMessage(HttpMethod.Post, TrustedEndpoint(await Metadata(ct), "token_endpoint"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.ClientId}:{config.ClientSecret}")));
        request.Content = new FormUrlEncodedContent(fields);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Keep only known OAuth codes: response bodies/descriptions can contain private data.
            var reason = "token-endpoint-error";
            try
            {
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (body.RootElement.ValueKind == JsonValueKind.Object && body.RootElement.TryGetProperty("error", out var error) &&
                    error.ValueKind == JsonValueKind.String)
                    reason = error.GetString() switch
                    {
                        "invalid_client" => "invalid-client",
                        "invalid_grant" => "invalid-grant",
                        "invalid_request" => "invalid-request",
                        "unauthorized_client" => "unauthorized-client",
                        _ => reason
                    };
            }
            catch (JsonException) { }
            throw new SsoException("EVE authentication failed. Reconnect this character.", reason, (int)response.StatusCode);
        }
        return (await response.Content.ReadFromJsonAsync<JsonObject>(ct))!;
    }

    public async Task<JwtSecurityToken> Validate(string token, CancellationToken ct)
    {
        var metadata = await Metadata(ct);
        async Task<IList<SecurityKey>> Keys() => (await cache.GetOrCreateAsync("sso-keys", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return new JsonWebKeySet(await http.GetStringAsync(TrustedEndpoint(metadata, "jwks_uri"), ct)).GetSigningKeys();
        }))!;
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true, RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateIssuer = true, ValidIssuers = ["https://login.eveonline.com", "https://login.eveonline.com/", "login.eveonline.com"],
            ValidateAudience = true,
            AudienceValidator = (audiences, _, _) => audiences.Contains(config.ClientId) && audiences.Contains("EVE Online"),
            ValidateLifetime = true, RequireExpirationTime = true, ClockSkew = TimeSpan.FromSeconds(30),
            IssuerSigningKeys = await Keys()
        };
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try { handler.ValidateToken(token, parameters, out var validated); return (JwtSecurityToken)validated; }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            cache.Remove("sso-keys");
            parameters.IssuerSigningKeys = await Keys();
            handler.ValidateToken(token, parameters, out var validated);
            return (JwtSecurityToken)validated;
        }
    }

    public string Protect(string token) => protector.Protect(token);

    // Called under the per-character gate. Access tokens never leave process memory.
    internal async Task<string> AccessToken(EveCharacter character, CancellationToken ct)
    {
        var key = $"access:{character.CharacterId}";
        if (cache.TryGetValue<string>(key, out var existing)) return existing!;
        string refresh;
        try { refresh = protector.Unprotect(character.RefreshToken); }
        catch (CryptographicException) { throw new SsoException("Token cannot be decrypted. Reconnect this character."); }
        var result = await Exchange(new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refresh }, ct);
        if (result["access_token"] is not JsonValue accessValue || !accessValue.TryGetValue<string>(out var access) || string.IsNullOrWhiteSpace(access))
            throw new SsoException("Incomplete token response.", "invalid-token-response");
        var jwt = await Validate(access, ct);
        if (jwt.Subject != $"CHARACTER:EVE:{character.CharacterId}") throw new SsoException("Character identity changed. Reconnect this character.");
        var owner = jwt.Claims.FirstOrDefault(c => c.Type == "owner")?.Value;
        if (character.CharacterOwnerHash is not null && owner != character.CharacterOwnerHash)
            throw new SsoException("Character ownership changed. Reconnect this character.");
        var grantedScopes = EveScopes.FromValidatedToken(jwt);
        if (result["refresh_token"] is { } rotated)
        {
            if (rotated is not JsonValue value || !value.TryGetValue<string>(out var refreshValue) || string.IsNullOrWhiteSpace(refreshValue))
                throw new SsoException("Incomplete token response.", "invalid-token-response");
            character.RefreshToken = Protect(refreshValue);
        }
        character.GrantedScopesJson = grantedScopes;
        await db.SaveChangesAsync(ct);
        if (jwt.ValidTo.AddSeconds(-60) > DateTime.UtcNow)
            cache.Set(key, access, jwt.ValidTo.AddSeconds(-60));
        return access;
    }
}
public class SsoException(string message, string reason = "authentication-error", int? statusCode = null) : Exception(message)
{
    public string Reason { get; } = reason;
    public int? StatusCode { get; } = statusCode;
}
