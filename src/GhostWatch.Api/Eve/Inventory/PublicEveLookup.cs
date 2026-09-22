namespace GhostWatch.Api.Eve.Inventory;

// Only public universe metadata. Never cache character-specific structures here.
public sealed class PublicEveLookup
{
    public string Key { get; set; } = "";
    public string Json { get; set; } = "{}";
    public DateTime ExpiresAt { get; set; }
}
