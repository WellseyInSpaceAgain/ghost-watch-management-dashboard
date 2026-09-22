namespace GhostWatch.Api.Economics.Capital;

public sealed class CapitalPool
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Role { get; set; } = "Other";
    public decimal AllocatedCapital { get; set; }
    public decimal? TargetCapital { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }
    public int Revision { get; set; } = 1;
}
public sealed class CapitalAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public Guid? FromPoolId { get; set; }
    public Guid? ToPoolId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
