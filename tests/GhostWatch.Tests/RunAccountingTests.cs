using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Economics;
using GhostWatch.Api.Economics.Capital;
using GhostWatch.Api.Economics.Charts;
using GhostWatch.Api.Economics.Reporting;
using GhostWatch.Api.Economics.Runs;
using GhostWatch.Api.Economics.Snapshots;
using GhostWatch.Api.Economics.Tracks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace GhostWatch.Tests;

public class RunAccountingTests
{
    private static EconomicRun Batch() => new()
    {
        Name = "T2 test", Status = "Active", ExpectedInputCost = 100, ExpectedJobCost = 10,
        ExpectedOtherCost = 20, ExpectedRevenue = 200, ActualInputCost = 110,
        ActualJobCost = 15, ActualOtherCost = 25, ActualRevenue = 250,
        ManufacturingHours = 24, ConcurrentSlots = 2, CapitalTiedUp = 200000
    };

    [Fact]
    public void Job_cost_is_separate_and_capital_efficiency_uses_only_explicit_capital()
    {
        var run = Batch(); var values = RunMetrics.Calculate(run);
        Assert.Equal(130, values.ExpectedCost); Assert.Equal(70, values.ExpectedProfit);
        Assert.Equal(150, values.ActualCost); Assert.Equal(100, values.ActualProfit);
        Assert.Equal(2, values.SlotDays); Assert.Equal(50, values.ProfitPerSlotDay);
        Assert.Equal(.00025m, values.CapitalEfficiency); Assert.Equal(150, values.Committed);
        run.CapitalTiedUp = 100000;
        Assert.Equal(.0005m, RunMetrics.Calculate(run).CapitalEfficiency);
        Assert.Equal(150, RunMetrics.Calculate(run).Committed);
        run.ActualJobCost = null;
        Assert.Null(RunMetrics.Calculate(run).ActualCost); Assert.Null(RunMetrics.Calculate(run).ActualProfit);
        Assert.Equal(130, RunMetrics.Calculate(run).Committed);
        run.ExpectedJobCost = null;
        Assert.Null(RunMetrics.Calculate(run).ExpectedCost); Assert.Null(RunMetrics.Calculate(run).ExpectedProfit);
        Assert.Null(RunMetrics.Calculate(run).Committed);
        run.ExpectedJobCost = 0; run.ActualJobCost = 0;
        Assert.Equal(120, RunMetrics.Calculate(run).ExpectedCost); Assert.Equal(135, RunMetrics.Calculate(run).ActualCost);
        run.ActualOtherCost = null;
        Assert.Null(RunMetrics.Calculate(run).ActualProfit);
        run.Status = "Completed";
        Assert.Equal(0, RunMetrics.Calculate(run).Committed);
    }

    [Theory]
    [InlineData("revenue")][InlineData("input")][InlineData("job")][InlineData("other")]
    [InlineData("hours")][InlineData("slots")][InlineData("zeroHours")][InlineData("negativeHours")]
    [InlineData("zeroSlots")][InlineData("capital")][InlineData("zeroCapital")][InlineData("negativeCapital")]
    public void Incomplete_or_invalid_inputs_leave_efficiency_unknown(string missing)
    {
        var run = Batch();
        switch (missing)
        {
            case "revenue": run.ActualRevenue = null; break;
            case "input": run.ActualInputCost = null; break;
            case "job": run.ActualJobCost = null; break;
            case "other": run.ActualOtherCost = null; break;
            case "hours": run.ManufacturingHours = null; break;
            case "slots": run.ConcurrentSlots = null; break;
            case "zeroHours": run.ManufacturingHours = 0; break;
            case "negativeHours": run.ManufacturingHours = -1; break;
            case "zeroSlots": run.ConcurrentSlots = 0; break;
            case "capital": run.CapitalTiedUp = null; break;
            case "zeroCapital": run.CapitalTiedUp = 0; break;
            case "negativeCapital": run.CapitalTiedUp = -1; break;
        }
        Assert.Null(RunMetrics.Calculate(run).CapitalEfficiency);
        Assert.Null(RunMetrics.AggregateCapitalEfficiency([Batch(), run]));
    }

    [Fact]
    public void Weighted_efficiency_is_not_an_arithmetic_average_and_preserves_losses()
    {
        var first = Batch(); var second = Batch(); second.ManufacturingHours = 72; second.CapitalTiedUp = 600000;
        // Profits 100 + 100; exposures 400,000 + 3,600,000.
        Assert.Equal(.00005m, RunMetrics.AggregateCapitalEfficiency([first, second]));
        Assert.NotEqual((RunMetrics.Calculate(first).CapitalEfficiency + RunMetrics.Calculate(second).CapitalEfficiency) / 2,
            RunMetrics.AggregateCapitalEfficiency([first, second]));
        Assert.Null(RunMetrics.AggregateCapitalEfficiency([]));
        first.ActualRevenue = 0; Assert.Equal(-.000375m, RunMetrics.Calculate(first).CapitalEfficiency);
        first.ActualRevenue = 150; Assert.Equal(0, RunMetrics.Calculate(first).CapitalEfficiency);
    }

    [Fact]
    public void Weighted_efficiency_handles_large_valid_capital_exposures()
    {
        var run = Batch(); run.ManufacturingHours = 24000000000000m; run.ConcurrentSlots = 1000;
        run.CapitalTiedUp = 1000000000000000m; run.ActualRevenue = 1000000000000000m;
        Assert.Equal(RunMetrics.Calculate(run).CapitalEfficiency, RunMetrics.AggregateCapitalEfficiency([run, run]));
    }

    [Fact]
    public async Task Api_persists_updates_and_clears_fields_and_updates_all_financial_reports()
    {
        await using var app = new TestApplication(); using var client = app.CreateClient();
        var run = Batch(); Guid poolId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
            var pool = new CapitalPool { Name = "Test capital", AllocatedCapital = 1000 };
            var track = new EconomyTrack { Name = "T2 Workshop", DefaultCapitalPoolId = pool.Id };
            db.CapitalPools.Add(pool); db.EconomyTracks.Add(track); await db.SaveChangesAsync();
            run.TrackId = track.Id; run.CapitalPoolId = poolId = pool.Id;
        }
        var created = await client.PostAsJsonAsync("/api/economics/runs", new RunInput(run)); created.EnsureSuccessStatusCode();
        run.Id = (await created.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<Guid>();
        async Task<JsonObject> Read() => (await client.GetFromJsonAsync<JsonObject>($"/api/economics/runs/{run.Id}"))!;
        var view = await Read();
        Assert.Equal(10, view["run"]!["expectedJobCost"]!.GetValue<decimal>());
        Assert.Equal(15, view["run"]!["actualJobCost"]!.GetValue<decimal>());
        Assert.Equal(200000, view["run"]!["capitalTiedUp"]!.GetValue<decimal>());
        Assert.Equal(.00025m, view["financials"]!["capitalEfficiency"]!.GetValue<decimal>());
        var pools = (await client.GetFromJsonAsync<JsonObject>("/api/economics/capital"))!;
        Assert.Equal(850, pools["metrics"]![poolId.ToString()]!["available"]!.GetValue<decimal>());
        Assert.Equal(150, pools["metrics"]![poolId.ToString()]!["lifetimeSpend"]!.GetValue<decimal>());
        run.ExpectedJobCost = 20; run.ActualJobCost = 25; run.CapitalTiedUp = 90000;
        run.Status = "Completed"; run.CompletedAt = DateTime.UtcNow; run.Verdict = "Scale";
        (await client.PutAsJsonAsync($"/api/economics/runs/{run.Id}", new RunInput(run))).EnsureSuccessStatusCode();
        run.Revision++;
        view = await Read();
        Assert.Equal(20, view["run"]!["expectedJobCost"]!.GetValue<decimal>());
        Assert.Equal(25, view["run"]!["actualJobCost"]!.GetValue<decimal>());
        Assert.Equal(90000, view["run"]!["capitalTiedUp"]!.GetValue<decimal>());
        Assert.Equal(.0005m, view["financials"]!["capitalEfficiency"]!.GetValue<decimal>());
        var overview = (await client.GetFromJsonAsync<JsonObject>("/api/economics/overview"))!;
        Assert.Equal(90, overview["summary"]!["metrics"]!["profit30d"]!.GetValue<decimal>());
        var operations = (await client.GetFromJsonAsync<JsonObject>($"/api/economics/tracks/{run.TrackId}/operations"))!;
        Assert.Equal(90, operations["summary"]!["metrics"]!["lifetimeProfit"]!.GetValue<decimal>());
        Assert.Equal(.0005m, operations["summary"]!["metrics"]!["capitalEfficiency"]!.GetValue<decimal>());
        (await client.PutAsJsonAsync($"/api/economics/tracks/{run.TrackId}/kpis", new KpiInput(["capitalEfficiency"]))).EnsureSuccessStatusCode();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
            var saved = await db.EconomicRuns.SingleAsync();
            Assert.Equal(20, saved.ExpectedOtherCost); Assert.Equal(25, saved.ActualOtherCost);
            Assert.Equal(90000, saved.CapitalTiedUp);
            var snapshot = await scope.ServiceProvider.GetRequiredService<SnapshotStore>().Capture(DateTime.UtcNow, false, "Revised accounting", null, default);
            Assert.Equal(2, snapshot.SchemaVersion);
            var captured = SnapshotStore.Values(snapshot).Tracks.Single();
            Assert.Equal(.0005m, captured.Metrics["capitalEfficiency"]);
            Assert.Equal(["capitalEfficiency"], captured.SelectedKpis);
            Assert.Equal(90, SnapshotStore.Values(snapshot).Metrics["profit30d"]);
        }
        foreach (var field in new[] { "expectedJobCost", "actualJobCost", "capitalTiedUp" })
        {
            var payload = JsonSerializer.SerializeToNode(run, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            foreach (var invalid in new[] { -1m, 1000000000000001m })
            {
                payload[field] = invalid;
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/economics/runs/{run.Id}", new { run = payload })).StatusCode);
            }
            payload[field] = "invalid";
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/economics/runs/{run.Id}", new { run = payload })).StatusCode);
        }
        run.ExpectedJobCost = null; run.ActualJobCost = null; run.CapitalTiedUp = null;
        (await client.PutAsJsonAsync($"/api/economics/runs/{run.Id}", new RunInput(run))).EnsureSuccessStatusCode();
        view = await Read();
        Assert.Null(view["run"]!["expectedJobCost"]); Assert.Null(view["run"]!["actualJobCost"]); Assert.Null(view["run"]!["capitalTiedUp"]);
        Assert.Null(view["financials"]!["actualProfit"]); Assert.Null(view["financials"]!["capitalEfficiency"]);
    }

    [Fact]
    public async Task Additive_migration_preserves_legacy_inputs_and_snapshot_json_without_backfill()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new GhostWatchDbContext(new DbContextOptionsBuilder<GhostWatchDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260923000536_AddChartDefinitionsAndPlacements");
        var track = new EconomyTrack { Name = "Historical Workshop" }; db.EconomyTracks.Add(track); await db.SaveChangesAsync();
        var id = Guid.NewGuid(); var now = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO EconomicRuns (Id, Name, TrackId, RunType, Purpose, Status, StartedAt, ExpectedInputCost, ExpectedOtherCost, ExpectedRevenue,
            ActualInputCost, ActualOtherCost, ActualRevenue, Verdict, Notes, CreatedAt, UpdatedAt, Revision)
            VALUES ({id}, 'Historical Run', {track.Id}, 'Manufacturing', 'Commercial', 'Active', {now}, 100, 20, 200, 110, 25, 250, 'Retest', 'Original notes', {now}, {now}, 1)
            """);
        // Literal version-1 payload: captured cost semantics predate Job Cost and the new KPI.
        var json = $$"""
            {"timestamp":"2026-09-23T00:00:00Z","metrics":{"profit30d":115},"facts":{"liquid":null,"knownLiquid":0,"walletCount":0,"characterCount":0,"walletsStale":false,"marketBuyCommitments":null,"sellOrderListedValue":null,"ordersStale":false},"pools":[],"tracks":[{"id":"{{track.Id}}","name":"Historical Workshop","status":"Active","purpose":"Other","poolId":null,"poolName":null,"metrics":{"lifetimeProfit":115,"profitPerSlotDay":57.5},"selectedKpis":["profitPerSlotDay"],"kpiRevision":1}],"replacementPackageId":null,"replacementPackageName":null,"objectives":[]}
            """;
        var old = new EconomicSnapshot { SchemaVersion = 1, Timestamp = now, Trigger = "AutomaticMonthly", MonthKey = "2026-09", ValuesJson = json }; db.EconomicSnapshots.Add(old); await db.SaveChangesAsync();
        await migrator.MigrateAsync(); db.ChangeTracker.Clear();
        Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var run = await db.EconomicRuns.SingleAsync();
        Assert.Null(run.ExpectedJobCost); Assert.Null(run.ActualJobCost); Assert.Null(run.CapitalTiedUp);
        Assert.Equal(100, run.ExpectedInputCost); Assert.Equal(20, run.ExpectedOtherCost); Assert.Equal(200, run.ExpectedRevenue);
        Assert.Equal(110, run.ActualInputCost); Assert.Equal(25, run.ActualOtherCost); Assert.Equal(250, run.ActualRevenue);
        Assert.Equal("Original notes", run.Notes); Assert.Equal("Retest", run.Verdict);
        Assert.Null(RunMetrics.Calculate(run).ActualProfit); Assert.Null(RunMetrics.Calculate(run).Committed);
        var stored = await db.EconomicSnapshots.SingleAsync(); Assert.Equal(1, stored.SchemaVersion); Assert.Equal(json, stored.ValuesJson);
        Assert.Equal(115, SnapshotStore.Values(stored).Metrics["profit30d"]);
        var sameMonth = await new SnapshotStore(db).Capture(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc), true, null, null, default);
        Assert.Equal(old.Id, sameMonth.Id); Assert.Equal(1, sameMonth.SchemaVersion); Assert.Equal(json, sameMonth.ValuesJson);
        // The HTTP reader also serves the unchanged version-1 payload.
        await using var app = new TestApplication(); using var client = app.CreateClient();
        using (var scope = app.Services.CreateScope())
        {
            var apiDb = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
            apiDb.EconomicSnapshots.Add(new EconomicSnapshot { Id = old.Id, SchemaVersion = 1, Timestamp = now, ValuesJson = json });
            await apiDb.SaveChangesAsync();
        }
        var apiView = (await client.GetFromJsonAsync<JsonObject>($"/api/economics/snapshots/{old.Id}"))!;
        Assert.Equal(1, apiView["schemaVersion"]!.GetValue<int>());
        Assert.Equal(115, apiView["values"]!["metrics"]!["profit30d"]!.GetValue<decimal>());
        var historicalConfig = ChartValidation.Parse(ChartJson("snapshots", "capitalEfficiency", track.Id));
        Assert.Null((await ChartQuery.Execute(historicalConfig, null, db, default)).Series[0].Values[0]);
        var oldConfig = ChartValidation.Parse(ChartJson("snapshots", "profitPerSlotDay", track.Id));
        Assert.Equal(57.5m, (await ChartQuery.Execute(oldConfig, null, db, default)).Series[0].Values[0]);
        Assert.Equal(json, (await db.EconomicSnapshots.AsNoTracking().SingleAsync()).ValuesJson);
    }

    [Fact]
    public async Task Track_efficiency_weights_realised_runs_and_keeps_incomplete_research_unknown()
    {
        await using var app = new TestApplication(); using var client = app.CreateClient();
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
        var track = new EconomyTrack { Name = "Weighted programme" }; db.EconomyTracks.Add(track);
        var first = Batch(); first.TrackId = track.Id; first.Status = "Completed"; first.CompletedAt = DateTime.UtcNow;
        var second = Batch(); second.TrackId = track.Id; second.Status = "Evaluated"; second.CompletedAt = DateTime.UtcNow;
        second.ManufacturingHours = 72; second.CapitalTiedUp = 600000;
        var research = new EconomicRun { Name = "Research", TrackId = track.Id, Purpose = "R&D", ActualInputCost = 80, ActualJobCost = 5, ActualOtherCost = 0 };
        db.EconomicRuns.AddRange(first, second, research); await db.SaveChangesAsync();
        var summary = await EconomicReporting.Capture(db, DateTime.UtcNow, default);
        Assert.Equal(.00005m, summary.Tracks.Single().Metrics["capitalEfficiency"]);
        Assert.Equal(85, summary.Tracks.Single().Metrics["rdSpend"]);
        Assert.Equal(200, summary.Tracks.Single().Metrics["lifetimeProfit"]);
        var trackChart = ChartValidation.Parse(ChartJson("tracks", "capitalEfficiency", track.Id));
        Assert.Equal(.00005m, (await ChartQuery.Execute(trackChart, null, db, default)).Series[0].Values[0]);
        research.Status = "Completed"; research.CompletedAt = DateTime.UtcNow; research.Verdict = "R&D Successful";
        await db.SaveChangesAsync();
        summary = await EconomicReporting.Capture(db, DateTime.UtcNow, default);
        Assert.Null(summary.Tracks.Single().Metrics["capitalEfficiency"]);
        Assert.Null(summary.Tracks.Single().Metrics["lifetimeProfit"]);
        Assert.Equal(85, summary.Tracks.Single().Metrics["rdSpend"]);
        Assert.Null((await ChartQuery.Execute(trackChart, null, db, default)).Series[0].Values[0]);
    }

    private static string ChartJson(string source, string field, Guid trackId) => JsonSerializer.Serialize(
        new ChartConfig("Capital comparison", null, "bar", source, new(TrackId: trackId.ToString()), new("name", "Run"),
            [new(field, "Profit / Slot-Day / ISK Tied Up", "latest", "ratio")]), ChartValidation.JsonOptions);

    [Fact]
    public async Task Saved_charts_accept_efficiency_and_legacy_definitions_and_keep_unknown_values()
    {
        await using var app = new TestApplication(); using var client = app.CreateClient(); Guid trackId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
            var track = new EconomyTrack { Name = "Charts" }; trackId = track.Id; db.EconomyTracks.Add(track);
            var run = Batch(); run.TrackId = trackId;
            var unknown = Batch(); unknown.Name = "Unknown capital"; unknown.TrackId = trackId; unknown.CapitalTiedUp = null;
            db.EconomicRuns.AddRange(run, unknown);
            db.ChartDefinitions.Add(new ChartDefinition { Name = "Previously saved chart", ConfigJson = ChartValidation.Sample });
            await db.SaveChangesAsync();
        }
        var config = ChartJson("runs", "capitalEfficiency", trackId);
        var response = await client.PostAsJsonAsync("/api/economics/charts/definitions", new ChartInput("Efficiency", config)); response.EnsureSuccessStatusCode();
        var definitions = (await client.GetFromJsonAsync<ChartDefinition[]>("/api/economics/charts/definitions"))!;
        Assert.Equal(2, definitions.Length);
        foreach (var definition in definitions) ChartValidation.Parse(definition.ConfigJson);
        var preview = await client.PostAsJsonAsync("/api/economics/charts/preview", new PreviewInput(config)); preview.EnsureSuccessStatusCode();
        var result = (await preview.Content.ReadFromJsonAsync<ChartData>())!;
        Assert.Equal(.00025m, result.Series[0].Values[Array.IndexOf(result.Labels, "T2 test")]);
        Assert.Null(result.Series[0].Values[Array.IndexOf(result.Labels, "Unknown capital")]);
        foreach (var aggregation in new[] { "sum", "average", "min", "max", "latest" })
        {
            var grouped = ChartValidation.Parse(config) with { X = new("all", null), Series = [new("capitalEfficiency", "Efficiency", aggregation, "ratio")] };
            // The incomplete Run is newest for the latest aggregation.
            var groupedResponse = await client.PostAsJsonAsync("/api/economics/charts/preview", new PreviewInput(JsonSerializer.Serialize(grouped, ChartValidation.JsonOptions)));
            groupedResponse.EnsureSuccessStatusCode(); Assert.Null((await groupedResponse.Content.ReadFromJsonAsync<ChartData>())!.Series[0].Values[0]);
        }
    }
}
