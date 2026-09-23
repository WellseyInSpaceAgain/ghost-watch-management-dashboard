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
 [Fact] public async Task Attention_rules_cover_execution_planning_allocation_and_stale_facts_without_penalising_research()
 {
  await using var app=new TestApplication();using var browser=app.CreateClient();using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
  var pool=new CapitalPool{Name="Capital",AllocatedCapital=100};var track=new EconomyTrack{Name="Workshop"};db.CapitalPools.Add(pool);db.EconomyTracks.Add(track);
  db.EveCharacters.Add(new(){CharacterId=42,CharacterName="Pilot"});
  db.EveSections.Add(new(){CharacterId=42,Name="wallet",Json="10",UpdatedAt=DateTime.UtcNow.AddDays(-2)});
  db.EveIndustryJobs.Add(new(){CharacterId=42,JobId=1,Status="active"});
  db.EconomicRuns.AddRange(new EconomicRun{Name="Missing sales",TrackId=track.Id,Status="Completed",CompletedAt=DateTime.UtcNow,ActualRevenue=null},new EconomicRun{Name="Research success",TrackId=track.Id,Purpose="R&D",Status="Completed",CompletedAt=DateTime.UtcNow,Verdict="R&D Successful",ActualRevenue=null},new EconomicRun{Name="Committed batch",TrackId=track.Id,CapitalPoolId=pool.Id,Status="Active",ExpectedInputCost=95,ExpectedOtherCost=0});
  db.Objectives.Add(new(){Name="Overdue gate",Type="Gate",Status="Active",TargetDate=DateTime.UtcNow.AddDays(-1),ConditionsJson="[{\"Label\":\"Demand\",\"Done\":false}]"});await db.SaveChangesAsync();
  var summary=await EconomicReporting.Capture(db,DateTime.UtcNow,default);var items=await EconomicReporting.Attention(db,summary,default);
  Assert.Equal(new[]{"incomplete-checklist","missing-sales","over-allocation","overdue-objective","pool-utilisation","unassociated-jobs","unevaluated-run","wallet-freshness"},items.Select(x=>x.Rule).Order().ToArray());
  Assert.DoesNotContain(items,x=>x.Message.Contains("Research success"));Assert.All(items,x=>Assert.StartsWith("/",x.Path));
 }

}
