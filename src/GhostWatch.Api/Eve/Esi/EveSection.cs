namespace GhostWatch.Api.Eve.Esi;

// Replaceable EVE facts only. No management annotations or relationships live here.
public sealed class EveSection
{
    public long CharacterId { get; set; }
    public string Name { get; set; } = "";
    public string? Json { get; set; }
    public DateTime AttemptedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? Error { get; set; }
}

public sealed class EveIndustryJob
{
    public long CharacterId { get; set; }
    public long JobId { get; set; }
    public int ActivityId { get; set; }
    public long BlueprintTypeId { get; set; }
    public long? ProductTypeId { get; set; }
    public int Runs { get; set; }
    public string Status { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime LastSeenAt { get; set; }
    public string RawJson { get; set; } = "{}";
}
