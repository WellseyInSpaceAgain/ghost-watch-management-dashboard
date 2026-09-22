using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GhostWatch.Api.Eve.Auth;

[ApiController, Route("api/auth/eve")]
public sealed class AuthController(SsoClient sso, PendingLogins logins, IMemoryCache cache,
    IOptions<EveOptions> options, GhostWatchDbContext db, CharacterGate gates, ILogger<AuthController> logger) : ControllerBase
{
    private const string Cookie = "ghost-watch-oauth";

    [HttpGet("config")]
    public IActionResult Config() => Ok(new { configured = options.Value.Configured,
        callbackUrl = options.Value.CallbackUrl, scopes = EveOptions.Scopes });

    [HttpGet("start")]
    public async Task<IActionResult> Start(long? characterId, CancellationToken ct)
    {
        if (characterId is not null && !await db.EveCharacters.AnyAsync(x => x.CharacterId == characterId, ct)) return NotFound();
        if (!options.Value.Configured) return Outcome("configuration", characterId);
        try
        {
            var endpoint = SsoClient.TrustedEndpoint(await sso.Metadata(ct), "authorization_endpoint");
            var state = RandomValue();
            var verifier = RandomValue();
            var browser = RandomValue();
            logins.Add(state, new(verifier, browser, characterId));
            Response.Cookies.Append(Cookie, browser, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps, MaxAge = TimeSpan.FromMinutes(10), Path = "/api/auth/eve" });
            return Redirect(QueryHelpers.AddQueryString(endpoint, new Dictionary<string, string?>
            {
                ["response_type"] = "code", ["client_id"] = options.Value.ClientId,
                ["redirect_uri"] = options.Value.CallbackUrl, ["scope"] = string.Join(' ', EveOptions.Scopes),
                ["state"] = state, ["code_challenge_method"] = "S256",
                ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            }));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        { return Failed(ex, characterId); }
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(string? code, string? state, string? error, CancellationToken ct)
    {
        var pending = logins.Consume(state, Request.Cookies[Cookie]);
        if (pending is null) return Outcome("invalid-state");
        Response.Cookies.Delete(Cookie, new CookieOptions { Path = "/api/auth/eve" });
        if (error is not null || string.IsNullOrWhiteSpace(code)) return Outcome("cancelled", pending.CharacterId);
        try
        {
            var tokens = await sso.Exchange(new() { ["grant_type"] = "authorization_code", ["code"] = code,
                ["code_verifier"] = pending.Verifier, ["redirect_uri"] = options.Value.CallbackUrl }, ct);
            if (tokens["access_token"] is not JsonValue accessValue || !accessValue.TryGetValue<string>(out var access) || string.IsNullOrWhiteSpace(access) ||
                tokens["refresh_token"] is not JsonValue refreshValue || !refreshValue.TryGetValue<string>(out var refresh) || string.IsNullOrWhiteSpace(refresh))
                throw new SsoException("Incomplete token response.", "invalid-token-response");
            var jwt = await sso.Validate(access, ct);
            if (!jwt.Subject.StartsWith("CHARACTER:EVE:", StringComparison.Ordinal) || !long.TryParse(jwt.Subject[14..], out var id) || id <= 0)
                throw new SsoException("Invalid character identity.", "invalid-character");
            if (pending.CharacterId is { } expected && expected != id)
                throw new SsoException("A different character was selected. Existing credentials were retained.", "wrong-character");
            var grantedScopes = EveScopes.FromValidatedToken(jwt);
            var name = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
            var owner = jwt.Claims.FirstOrDefault(c => c.Type == "owner")?.Value;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(owner))
                throw new SsoException("Incomplete character identity.", "invalid-character");
            var gate = gates.For(id);
            await gate.WaitAsync(ct);
            try
            {
                var character = await db.EveCharacters.SingleOrDefaultAsync(c => c.CharacterId == id, ct);
                if (character is null && pending.CharacterId is not null) throw new SsoException("Character no longer exists.", "invalid-character");
                if (character is not null) await db.Entry(character).ReloadAsync(ct);
                if (character is null) { character = new() { CharacterId = id }; db.EveCharacters.Add(character); }
                // Do not silently rebind a known character to a different owner or alter local history.
                else if (character.CharacterOwnerHash is not null && character.CharacterOwnerHash != owner)
                    throw new SsoException("Character ownership changed.", "ownership-changed");
                character.CharacterName = name;
                character.CharacterOwnerHash = owner;
                character.RefreshToken = sso.Protect(refresh);
                character.GrantedScopesJson = grantedScopes;
                character.LastAuthenticatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                cache.Remove($"access:{id}");
            }
            finally { gate.Release(); }
            return Outcome(pending.CharacterId is null ? "connected" : "reauthorised", pending.CharacterId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        { return Failed(ex, pending.CharacterId); }
    }

    private IActionResult Failed(Exception ex, long? characterId = null)
    {
        var reason = ex switch
        {
            SsoException ssoError => ssoError.Reason,
            SecurityTokenException => "invalid-token",
            HttpRequestException => "network-error",
            OperationCanceledException => "timeout",
            _ => "unexpected-error"
        };
        // Never log exception messages, tokens, callback query strings or raw OAuth errors.
        logger.LogWarning("EVE SSO failed: {Reason} ({ErrorType})", reason, ex.GetType().Name);
        return Outcome(reason, characterId);
    }
    private RedirectResult Outcome(string result, long? characterId = null) =>
        Redirect($"/characters{(characterId is null ? "" : $"/{characterId}")}?auth={result}");
    private static string RandomValue() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
}
