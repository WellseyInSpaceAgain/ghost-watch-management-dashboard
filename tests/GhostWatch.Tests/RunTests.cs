using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Economics.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace GhostWatch.Tests;
public class RunTests
{
    [Fact]
    public async Task Manual_and_assisted_runs_keep_estimates_results_and_job_links_separate()
    {
        await using var app = new TestApplication(); using var browser = app.CreateClient();
        Guid trackId, poolId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
            var track = new GhostWatch.Api.Economics.Tracks.EconomyTrack { Name = "Workshop" }; db.EconomyTracks.Add(track); trackId = track.Id;
            var pool = new GhostWatch.Api.Economics.Capital.CapitalPool { Name = "Workshop pool", AllocatedCapital = 500 }; db.CapitalPools.Add(pool); poolId = pool.Id;
            db.EveCharacters.Add(new() { CharacterId = 7, CharacterName = "Industry pilot" });
            foreach (var id in new long[] { 41, 42 }) db.EveIndustryJobs.Add(new() { CharacterId = 7, JobId = id, BlueprintTypeId = 100, ProductTypeId = 101, Runs = 10, Status = "active", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), RawJson = "{}" });
            await db.SaveChangesAsync();
        }
        var run = new EconomicRun { Name = "Manual batch", TrackId = trackId, CapitalPoolId = poolId, Status = "Active", ExpectedInputCost = 200, ExpectedOtherCost = 20, ExpectedRevenue = 300, ActualInputCost = 210, ActualOtherCost = 30, ActualRevenue = 330, Notes = "Keep this manual estimate", ManufacturingHours = 24, ConcurrentSlots = 2 };
        var response = await browser.PostAsJsonAsync("/api/economics/runs", new RunInput(run)); response.EnsureSuccessStatusCode();
        var idValue = (await response.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<Guid>();
        var view = (await browser.GetFromJsonAsync<JsonObject>($"/api/economics/runs/{idValue}"))!;
        Assert.Equal(80, view["financials"]!["expectedProfit"]!.GetValue<decimal>());
        Assert.Equal(90, view["financials"]!["actualProfit"]!.GetValue<decimal>());
        Assert.Equal(2, view["financials"]!["slotDays"]!.GetValue<decimal>());
        Assert.Equal(45, view["financials"]!["profitPerSlotDay"]!.GetValue<decimal>());
        (await browser.PostAsJsonAsync($"/api/economics/runs/{idValue}/jobs", new JobReference(7, 41))).EnsureSuccessStatusCode();
        (await browser.PostAsJsonAsync($"/api/economics/runs/{idValue}/jobs", new JobReference(7, 42))).EnsureSuccessStatusCode();
        var linked = (await browser.GetFromJsonAsync<JsonArray>($"/api/eve/industry-jobs?runId={idValue}"))!;
        Assert.Equal(2, linked.Count); Assert.Equal("Manual batch", linked[0]!["runName"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Conflict, (await browser.PostAsJsonAsync($"/api/economics/runs/{idValue}/jobs", new JobReference(7, 41))).StatusCode);
        (await browser.DeleteAsync($"/api/economics/runs/{idValue}/jobs/7/41")).EnsureSuccessStatusCode();
        var manual = new EconomicRun { Name = "Assisted experiment", TrackId = trackId, RunType = "R&D", Purpose = "R&D", Status = "Completed", Verdict = "R&D Successful", StartedAt = DateTime.UtcNow.AddDays(-1), CompletedAt = DateTime.UtcNow, ActualInputCost = 80, ActualOtherCost = 0 };
        (await browser.PostAsJsonAsync("/api/economics/runs", new RunInput(manual, new(7, 41)))).EnsureSuccessStatusCode();
        var summary = (await browser.GetFromJsonAsync<JsonObject>("/api/economics/capital"))!;
        Assert.Equal(240, summary["metrics"]![poolId.ToString()]!["committed"]!.GetValue<decimal>());
        Assert.Equal(260, summary["metrics"]![poolId.ToString()]!["available"]!.GetValue<decimal>());
        using var check = app.Services.CreateScope(); var saved = check.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
        Assert.Equal(2, await saved.EveIndustryJobs.CountAsync()); Assert.Equal(2, await saved.RunJobs.CountAsync());
        Assert.Equal("Keep this manual estimate", (await saved.EconomicRuns.FindAsync(idValue))!.Notes);
        var research = await saved.EconomicRuns.SingleAsync(x => x.Name == "Assisted experiment");
        Assert.Null(RunMetrics.Calculate(research).ActualProfit); Assert.Equal("R&D Successful", research.Verdict);
    }
    [Fact]
    public void Missing_duration_and_costs_remain_unknown_and_expected_never_overwrites_actual()
    {
        var run = new EconomicRun { Status = "Active", ExpectedInputCost = 10, ExpectedOtherCost = 0, ExpectedRevenue = 20 };
        var metrics = RunMetrics.Calculate(run); Assert.Equal(10, metrics.ExpectedProfit); Assert.Null(metrics.ActualProfit); Assert.Null(metrics.SlotDays); Assert.Equal(10, metrics.Committed);
        run.ExpectedOtherCost = null; Assert.Null(RunMetrics.Calculate(run).Committed);
        run.Status = "Completed"; Assert.Equal(0, RunMetrics.Calculate(run).Committed);
    }
}
