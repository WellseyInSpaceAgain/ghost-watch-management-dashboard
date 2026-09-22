using System.Text.Json.Serialization;
using GhostWatch.Api.Economics.Capital;
using GhostWatch.Api.Economics.Tracks;
using GhostWatch.Api.Eve.Esi;
namespace GhostWatch.Api.Economics.Runs;

public sealed class EconomicRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public Guid TrackId { get; set; }
    [JsonIgnore] public EconomyTrack Track { get; set; } = null!;
    public Guid? CapitalPoolId { get; set; }
    [JsonIgnore] public CapitalPool? CapitalPool { get; set; }
    public string RunType { get; set; } = "Other";
    public string Purpose { get; set; } = "Commercial";
    public string Status { get; set; } = "Planning";
    public long? ProductTypeId { get; set; }
    public string? ProductName { get; set; }
    public decimal? Quantity { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public decimal? ExpectedInputCost { get; set; }
    public decimal? ExpectedOtherCost { get; set; }
    public decimal? ExpectedRevenue { get; set; }
    public decimal? ActualInputCost { get; set; }
    public decimal? ActualOtherCost { get; set; }
    public decimal? ActualRevenue { get; set; }
    public decimal? ManufacturingHours { get; set; }
    public int? ConcurrentSlots { get; set; }
    public decimal? TimeToSellDays { get; set; }
    public string Verdict { get; set; } = "No Verdict";
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int Revision { get; set; } = 1;
}
public sealed class RunJob
{
    public long CharacterId { get; set; }
    public long JobId { get; set; }
    public Guid RunId { get; set; }
    public EconomicRun Run { get; set; } = null!;
    public EveIndustryJob Job { get; set; } = null!;
}
