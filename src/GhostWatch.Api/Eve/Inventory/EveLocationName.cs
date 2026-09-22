namespace GhostWatch.Api.Eve.Inventory;

// Structure permissions are character-specific; never share this cache across characters.
public sealed class EveLocationName
{
    public long CharacterId { get; set; }
    public long LocationId { get; set; }
    public string? Name { get; set; }
    public DateTime ExpiresAt { get; set; }
}
