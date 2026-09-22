using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Economics.Capital;
using GhostWatch.Api.Economics.Tracks;
using GhostWatch.Api.Economics.Runs;
using GhostWatch.Api.Economics.Reporting;
using GhostWatch.Api.Economics.Planning;
using Microsoft.Extensions.DependencyInjection;
namespace GhostWatch.Tests;
public class ReportingTests
{
 [Fact] public async Task Programme_and_track_metrics_keep_unknowns_and_shared_pool_commitments_distinct()
 {
  await using var app=new TestApplication();using var browser=app.CreateClient();Guid trackId;
  using(var scope=app.Services.CreateScope()){
   var db=scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();var pool=new CapitalPool{Name="Workshop fund",Role="Core Capital",AllocatedCapital=1000};var track=new EconomyTrack{Name="Workshop",Status="Active",DefaultCapitalPoolId=pool.Id};var other=new EconomyTrack{Name="Other",DefaultCapitalPoolId=pool.Id};trackId=track.Id;
   db.CapitalPools.Add(pool);db.EconomyTracks.AddRange(track,other);
   db.EconomicRuns.AddRange(new EconomicRun{Name="Sale",TrackId=track.Id,CapitalPoolId=pool.Id,Status="Completed",StartedAt=DateTime.UtcNow.AddDays(-5),CompletedAt=DateTime.UtcNow.AddDays(-1),ActualInputCost=100,ActualOtherCost=20,ActualRevenue=200,ManufacturingHours=24,ConcurrentSlots=2},new EconomicRun{Name="Working",TrackId=other.Id,CapitalPoolId=pool.Id,Status="Active",ExpectedInputCost=900,ExpectedOtherCost=0},new EconomicRun{Name="Experiment",TrackId=track.Id,Status="Planning",Purpose="R&D"});
   db.Objectives.Add(new Objective{Name="Readiness",TrackId=track.Id,ConditionsJson="[{\"Label\":\"Test batch\",\"Done\":false}]"});await db.SaveChangesAsync();
  }
  var overview=(await browser.GetFromJsonAsync<JsonObject>("/api/economics/overview"))!;
  Assert.Equal(80,overview["summary"]!["metrics"]!["profit30d"]!.GetValue<decimal>());Assert.Null(overview["summary"]!["metrics"]!["treasury"]);
  Assert.Equal(3,overview["attention"]!.AsArray().Count); // verdict, 90% pool use, checklist
  var view=(await browser.GetFromJsonAsync<JsonObject>($"/api/economics/tracks/{trackId}/operations"))!;
  var metrics=view["summary"]!["metrics"]!;Assert.Equal(0,metrics["capitalCommitted"]!.GetValue<decimal>());Assert.Equal(100,metrics["capitalAvailable"]!.GetValue<decimal>());Assert.Equal(40,metrics["profitPerSlotDay"]!.GetValue<decimal>());Assert.Null(metrics["rdSpend"]);
  (await browser.PutAsJsonAsync($"/api/economics/tracks/{trackId}/kpis",new KpiInput(["rdSpend","profitPerSlotDay"]))).EnsureSuccessStatusCode();
  Assert.Equal(HttpStatusCode.Conflict,(await browser.PutAsJsonAsync($"/api/economics/tracks/{trackId}/kpis",new KpiInput([]))).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await browser.PutAsJsonAsync($"/api/economics/tracks/{trackId}/kpis",new KpiInput(["madeUp"],1))).StatusCode);
 }
}
