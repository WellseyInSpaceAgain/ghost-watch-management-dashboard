namespace GhostWatch.Api.Economics.Tracks;

// Local management data. ESI refresh services must never replace this entity.
public sealed class EconomyTrack
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "Planning";
    public string Purpose { get; set; } = "Other";
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }
    public int Revision { get; set; } = 1;
}
