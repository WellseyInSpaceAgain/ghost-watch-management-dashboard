namespace GhostWatch.Api.Economics;

// Shared economic calculations: null means incomplete, never an implicit zero.
public static class FinancialMath
{
    public static decimal? Cost(decimal? input, decimal? job, decimal? other) => input + job + other;
    public static decimal? Profit(decimal? input, decimal? job, decimal? other, decimal? revenue) => revenue - Cost(input, job, other);
    public static decimal? Margin(decimal? profit, decimal? revenue) => revenue > 0 ? profit / revenue * 100 : null;
    public static decimal? SlotDays(decimal? hours, int? slots) => hours >= 0 && slots > 0 ? hours * slots / 24 : null;
    public static decimal? PerSlotDay(decimal? profit, decimal? slotDays) => slotDays > 0 ? profit / slotDays : null;
    public static decimal? CapitalEfficiency(decimal? profit, decimal? slotDays, decimal? capitalTiedUp) =>
        capitalTiedUp > 0 ? PerSlotDay(profit, slotDays) / capitalTiedUp : null;
    public static decimal? Coverage(decimal? treasury, decimal? replacementValue) => replacementValue > 0 ? treasury / replacementValue : null;
    public static decimal? CompleteSum(IEnumerable<decimal?> values)
    {
        var rows = values.ToArray();
        return rows.Any(x => x is null) ? null : rows.Sum(x => x!.Value);
    }
}
