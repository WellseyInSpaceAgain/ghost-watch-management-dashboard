using System.Text.Json.Serialization;
using GhostWatch.Api.Economics.Tracks;
namespace GhostWatch.Api.Economics.Planning;
public sealed record ChecklistItem(string Label, bool Done);
public sealed record ProductionStage(string Name, bool Internal);
public sealed class Objective
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid? TrackId { get; set; }
    [JsonIgnore] public EconomyTrack? Track { get; set; }
    public string Type { get; set; } = "Objective";
    public string Status { get; set; } = "Active";
    public DateTime? TargetDate { get; set; }
    public decimal? ManualProgress { get; set; }
    public string ConditionsJson { get; set; } = "[]";
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int Revision { get; set; } = 1;
}
public sealed class TrackStrategy
{
    public Guid TrackId { get; set; }
    public string StagesJson { get; set; } = "[]";
    public int Revision { get; set; } = 1;
}
