using System.Text.Json.Serialization;

namespace GhostWatch.Api.Eve;

// Authenticated EVE identity; local planning belongs in separate Economics entities.
public sealed class EveCharacter
{
    public long CharacterId { get; set; }
    public string CharacterName { get; set; } = "";
    [JsonIgnore] public string? CharacterOwnerHash { get; set; }
    [JsonIgnore] public string RefreshToken { get; set; } = "";
    // Null means this pre-existing connection has not had its grants verified yet.
    [JsonIgnore] public string? GrantedScopesJson { get; set; }
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAuthenticatedAt { get; set; } = DateTime.UtcNow;
}
