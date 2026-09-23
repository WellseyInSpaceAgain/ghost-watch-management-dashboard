namespace GhostWatch.Api.Economics.Runs;
public sealed record RunFinancials(decimal? ExpectedCost, decimal? ExpectedProfit, decimal? ActualCost, decimal? ActualProfit,
    decimal? Margin, decimal? SlotDays, decimal? ProfitPerSlotDay, decimal? CapitalTurnDays, decimal? TimeToSellDays, decimal? Committed, decimal? CapitalEfficiency);
public static class RunMetrics
{
    public static bool Realised(EconomicRun run) => run.Status is "Completed" or "Evaluated";
    public static RunFinancials Calculate(EconomicRun run)
    {
        var expectedCost = FinancialMath.Cost(run.ExpectedInputCost, run.ExpectedJobCost, run.ExpectedOtherCost);
        var actualCost = FinancialMath.Cost(run.ActualInputCost, run.ActualJobCost, run.ActualOtherCost);
        var profit = FinancialMath.Profit(run.ActualInputCost, run.ActualJobCost, run.ActualOtherCost, run.ActualRevenue);
        var days = FinancialMath.SlotDays(run.ManufacturingHours, run.ConcurrentSlots);
        return new(expectedCost, FinancialMath.Profit(run.ExpectedInputCost, run.ExpectedJobCost, run.ExpectedOtherCost, run.ExpectedRevenue), actualCost, profit,
            FinancialMath.Margin(profit, run.ActualRevenue), days, FinancialMath.PerSlotDay(profit, days),
            run.CompletedAt is { } completed ? (decimal)(completed - run.StartedAt).TotalDays : null, run.TimeToSellDays,
            run.Status is "Active" or "Selling" ? actualCost ?? expectedCost : 0,
            FinancialMath.CapitalEfficiency(profit, days, run.CapitalTiedUp));
    }
    // Weight each realised Run ratio by its capital × slot-days exposure, not by Run count.
    // All Runs in the supplied cohort must have valid inputs; none are silently excluded.
    public static decimal? AggregateCapitalEfficiency(IEnumerable<EconomicRun> runs)
    {
        var rows = runs.Select(run => (Run: run, Metrics: Calculate(run))).ToArray();
        if (rows.Length == 0 || rows.Any(x => x.Metrics.CapitalEfficiency is null)) return null;
        var profit = FinancialMath.CompleteSum(rows.Select(x => x.Metrics.ActualProfit));
        // Normalize capital before multiplying: allowed amounts × slot-days can exceed decimal range.
        var capitalScale = rows.Max(x => x.Run.CapitalTiedUp)!.Value;
        var exposure = FinancialMath.CompleteSum(rows.Select(x => x.Metrics.SlotDays * (x.Run.CapitalTiedUp / capitalScale)));
        return exposure > 0 ? profit / exposure / capitalScale : null;
    }
    public static object PoolSummary(decimal allocated, IEnumerable<EconomicRun> runs)
    {
        var rows = runs.ToArray(); var values = rows.Select(Calculate).ToArray();
        var committed = FinancialMath.CompleteSum(values.Select(x => x.Committed));
        var realised = rows.Where(Realised).Select(Calculate).ToArray();
        return new { committed, available = allocated - committed,
            lifetimeSpend = rows.Length == 0 ? null : FinancialMath.CompleteSum(values.Select(x => x.ActualCost)),
            lifetimeRevenue = rows.Length == 0 ? null : FinancialMath.CompleteSum(rows.Select(x => x.ActualRevenue)),
            lifetimeProfit = realised.Length == 0 ? null : FinancialMath.CompleteSum(realised.Select(x => x.ActualProfit)),
            activeRuns = rows.Count(x => x.Status is "Active" or "Selling") };
    }
}
