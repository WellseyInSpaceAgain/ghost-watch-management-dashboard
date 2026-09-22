using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

namespace GhostWatch.Api.Eve.Auth;

public sealed record ScopeDefinition(string Operation, string Name);
public sealed record EvePermissions(bool ScopesKnown, bool HasAllRequiredScopes, int MissingScopeCount, string[] MissingScopes);

public static class EveScopes
{
    // Single source for both requested permissions and the operations that consume them.
    public static IReadOnlyList<ScopeDefinition> Definitions { get; } = Array.AsReadOnly<ScopeDefinition>([
        new("skills", "esi-skills.read_skills.v1"), new("skillQueue", "esi-skills.read_skillqueue.v1"),
        new("industryJobs", "esi-industry.read_character_jobs.v1"), new("blueprints", "esi-characters.read_blueprints.v1"),
        new("assets", "esi-assets.read_assets.v1"), new("wallet", "esi-wallet.read_character_wallet.v1"),
        new("marketOrders", "esi-markets.read_character_orders.v1"), new("standings", "esi-characters.read_standings.v1"),
        new("loyalty", "esi-characters.read_loyalty.v1"), new("planets", "esi-planets.manage_planets.v1"),
        new("structures", "esi-universe.read_structures.v1")
    ]);
    public static IEnumerable<string> Required => Definitions.Select(x => x.Name);
    public static string ForOperation(string operation) => Definitions.Single(x => x.Operation == operation).Name;
    public static string Store(IEnumerable<string> scopes) => JsonSerializer.Serialize(scopes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    // Only call on a token after signature, issuer, audience, lifetime and identity validation.
    public static string FromValidatedToken(JwtSecurityToken token)
    {
        if (!token.Payload.TryGetValue("scp", out var value)) return Store([]);
        var claim = JsonSerializer.SerializeToElement(value);
        var scopes = claim.ValueKind switch
        {
            JsonValueKind.String => claim.GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            JsonValueKind.Array when claim.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String) => claim.EnumerateArray().Select(x => x.GetString()!).ToArray(),
            _ => throw new SsoException("Invalid granted permissions in EVE token.", "invalid-token")
        };
        if (scopes.Any(x => string.IsNullOrWhiteSpace(x) || x.Any(char.IsWhiteSpace)))
            throw new SsoException("Invalid granted permissions in EVE token.", "invalid-token");
        return Store(scopes);
    }

    public static EvePermissions Permissions(string? grantedJson) => Compare(grantedJson, Required);
    public static EvePermissions Compare(string? grantedJson, IEnumerable<string> required)
    {
        var granted = grantedJson is null ? [] : JsonSerializer.Deserialize<string[]>(grantedJson)!;
        var missing = required.Except(granted, StringComparer.Ordinal).ToArray();
        return new(grantedJson is not null, grantedJson is not null && missing.Length == 0, missing.Length, missing);
    }
    public static bool Allows(string? grantedJson, string operation) => Compare(grantedJson, [ForOperation(operation)]).HasAllRequiredScopes;
    public static string MissingMessage(string operation) => $"Re-authorise this character to grant {ForOperation(operation)}. Previously collected data is retained.";
}
