using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Economics.Capital;
using GhostWatch.Api.Economics.Planning;
using GhostWatch.Api.Economics.Reporting;
using GhostWatch.Api.Economics.Runs;
using GhostWatch.Api.Economics.Tracks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GhostWatch.Tests;

public class AttentionTests
{
 private const string Endpoint="/api/economics/attention";
 private static async Task<AttentionState> Read(HttpClient client)=>(await client.GetFromJsonAsync<AttentionState>(Endpoint))!;
 private static async Task<AcknowledgedAttention> Accept(HttpClient client,AttentionItem item)
 {
  var response=await client.PostAsJsonAsync(Endpoint+"/acknowledgements",new AcknowledgeInput(item.Key));
  response.EnsureSuccessStatusCode();return (await response.Content.ReadFromJsonAsync<AcknowledgedAttention>())!;
 }

 [Fact]
 public async Task Over_allocation_acceptance_persists_across_host_restart_without_changing_economic_state_and_can_be_restored()
 {
  var directory=Path.Combine(Path.GetTempPath(),"ghost-watch-attention-"+Guid.NewGuid());
  AcknowledgedAttention accepted;string before;
  try
  {
   await using(var app=new TestApplication(builder=>builder.UseSetting("Storage:Directory",directory)))
   {
    using var client=app.CreateClient();
    using(var scope=app.Services.CreateScope())
    {
     var db=scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
     db.EveCharacters.Add(new(){CharacterId=42,CharacterName="Pilot"});
     db.EveSections.Add(new(){CharacterId=42,Name="wallet",Json="10",UpdatedAt=DateTime.UtcNow});
     var pool=new CapitalPool{Name="Conceptual reserve",AllocatedCapital=100};
     var track=new EconomyTrack{Name="Workshop",DefaultCapitalPoolId=pool.Id};
     db.CapitalPools.Add(pool);db.EconomyTracks.Add(track);
     db.EconomicRuns.Add(new(){Name="Measured batch",TrackId=track.Id,CapitalPoolId=pool.Id,Status="Completed",CompletedAt=DateTime.UtcNow,ActualInputCost=10,ActualJobCost=2,ActualOtherCost=3,ActualRevenue=30,ManufacturingHours=24,ConcurrentSlots=1,CapitalTiedUp=50,Verdict="Scale"});
     db.Objectives.Add(new(){Name="Planning",Status="Planning"});await db.SaveChangesAsync();
     before=await StoredState(db);
    }
    var warning=Assert.Single((await Read(client)).Active);
    Assert.Equal("over-allocation",warning.Rule);
    accepted=await Accept(client,warning);
    Assert.True(accepted.Matches);Assert.Equal(DateTimeKind.Utc,accepted.AcknowledgedAt.Kind);
    Assert.Empty((await Read(client)).Active);
    var overview=(await client.GetFromJsonAsync<JsonObject>("/api/economics/overview"))!;
    Assert.Empty(overview["attention"]!.AsArray());Assert.Single(overview["acknowledged"]!.AsArray());
    Assert.Equal(100,overview["summary"]!["metrics"]!["allocated"]!.GetValue<decimal>());
    Assert.Equal(.3m,overview["summary"]!["tracks"]![0]!["metrics"]!["capitalEfficiency"]!.GetValue<decimal>());
   }
   // New application host and database connections, using only this test's temporary storage.
   await using(var app=new TestApplication(builder=>builder.UseSetting("Storage:Directory",directory)))
   {
    using var client=app.CreateClient();using var scope=app.Services.CreateScope();
    var db=scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
    Assert.Equal(before,await StoredState(db));
    Assert.False(db.Database.HasPendingModelChanges());Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    var state=await Read(client);Assert.Empty(state.Active);
    Assert.Equal(accepted,Assert.Single(state.Acknowledged));
    var summary=await EconomicReporting.Capture(db,DateTime.UtcNow,default);
    Assert.Equal("over-allocation",Assert.Single(await EconomicReporting.Attention(db,summary,default)).Rule);
    (await client.DeleteAsync(Endpoint+"/acknowledgements/"+accepted.Id)).EnsureSuccessStatusCode();
    Assert.Equal("over-allocation",Assert.Single((await Read(client)).Active).Rule);
    Assert.Empty((await Read(client)).Acknowledged);Assert.Equal(before,await StoredState(db));
   }
  }
  finally{SqliteConnection.ClearAllPools();if(Directory.Exists(directory))Directory.Delete(directory,true);}
 }

 [Theory]
 [InlineData("run","missing-sales")]
 [InlineData("run","unevaluated-run")]
 [InlineData("objective","incomplete-checklist")]
 [InlineData("objective","overdue-objective")]
 [InlineData("pool","pool-utilisation")]
 public async Task Identity_is_per_rule_and_entity_and_survives_renames(string subjectType,string rule)
 {
  await using var app=new TestApplication();using var client=app.CreateClient();
  using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
  var track=new EconomyTrack{Name="Workshop"};db.EconomyTracks.Add(track);
  for(var i=0;i<2;i++)
  {
   var pool=new CapitalPool{Name="Same name",AllocatedCapital=100};db.CapitalPools.Add(pool);
   db.EconomicRuns.Add(new(){Name="Same name",TrackId=track.Id,Status="Completed",CompletedAt=DateTime.UtcNow});
   db.EconomicRuns.Add(new(){Name="Working",TrackId=track.Id,CapitalPoolId=pool.Id,Status="Active",ExpectedInputCost=95,ExpectedJobCost=0,ExpectedOtherCost=0});
   db.Objectives.Add(new(){Name="Same name",Status="Active",TargetDate=DateTime.UtcNow.AddDays(-1),ConditionsJson="[{\"Label\":\"Ready\",\"Done\":false}]"});
  }
  await db.SaveChangesAsync();var original=(await Read(client)).Active;
  var pair=original.Where(x=>x.Rule==rule).ToArray();Assert.Equal(2,pair.Length);Assert.NotEqual(pair[0].Key,pair[1].Key);
  var accepted=await Accept(client,pair[0]);Assert.Equal(subjectType,accepted.Item.SubjectType);
  var state=await Read(client);Assert.Equal(original.Count-1,state.Active.Count);
  Assert.Contains(state.Active,x=>x.Key==pair[1].Key);Assert.DoesNotContain(state.Active,x=>x.Key==pair[0].Key);
  Assert.Equal(pair[0].Key,Assert.Single(state.Acknowledged).Item.Key);
  var id=Guid.Parse(pair[0].SubjectId!);
  if(subjectType=="run")(await db.EconomicRuns.FindAsync(id))!.Name="Renamed";
  if(subjectType=="objective")(await db.Objectives.FindAsync(id))!.Name="Renamed";
  if(subjectType=="pool")(await db.CapitalPools.FindAsync(id))!.Name="Renamed";
  await db.SaveChangesAsync();state=await Read(client);
  Assert.Contains("Renamed",Assert.Single(state.Acknowledged).Item.Message);
  Assert.DoesNotContain(state.Active,x=>x.Key==pair[0].Key);
 }

 [Fact]
 public async Task Cleared_and_recurring_conditions_are_explicit_and_deleted_subjects_do_not_suppress_replacements()
 {
  await using var app=new TestApplication();using var client=app.CreateClient();
  using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
  var objective=new Objective{Name="Readiness",ConditionsJson="[{\"Label\":\"Ready\",\"Done\":false}]"};
  db.Objectives.Add(objective);await db.SaveChangesAsync();
  var item=Assert.Single((await Read(client)).Active);var accepted=await Accept(client,item);
  objective.Status="Completed";await db.SaveChangesAsync();
  var cleared=await Read(client);Assert.Empty(cleared.Active);Assert.False(Assert.Single(cleared.Acknowledged).Matches);
  Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(Endpoint+"/acknowledgements",new AcknowledgeInput(item.Key))).StatusCode);
  objective.Status="Active";await db.SaveChangesAsync();
  var recurring=await Read(client);Assert.Empty(recurring.Active);Assert.True(Assert.Single(recurring.Acknowledged).Matches);
  Assert.Equal(accepted.Id,Assert.Single(recurring.Acknowledged).Id);
  db.Objectives.Remove(objective);db.Objectives.Add(new(){Name=objective.Name,ConditionsJson=objective.ConditionsJson});await db.SaveChangesAsync();
  var replacement=await Read(client);Assert.NotEqual(item.Key,Assert.Single(replacement.Active).Key);
  Assert.False(Assert.Single(replacement.Acknowledged).Matches);
  (await client.DeleteAsync(Endpoint+"/acknowledgements/"+accepted.Id)).EnsureSuccessStatusCode();
  Assert.Single((await Read(client)).Active);Assert.Empty((await Read(client)).Acknowledged);
 }

 [Fact]
 public async Task Only_generated_findings_can_be_acknowledged_and_stale_actions_cannot_overwrite_acceptance()
 {
  await using var app=new TestApplication();using var client=app.CreateClient();
  using(var scope=app.Services.CreateScope())
  {
   var db=scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
   db.Objectives.Add(new(){Name="Readiness",ConditionsJson="[{\"Label\":\"Ready\",\"Done\":false}]"});await db.SaveChangesAsync();
  }
  Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Endpoint+"/acknowledgements",new AcknowledgeInput(null))).StatusCode);
  foreach(var key in new[]{"arbitrary-rule:programme:all","over-allocation:programme:all","incomplete-checklist:objective:"+Guid.NewGuid()})
   Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(Endpoint+"/acknowledgements",new AcknowledgeInput(key))).StatusCode);
  var item=Assert.Single((await Read(client)).Active);var first=await Accept(client,item);
  Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(Endpoint+"/acknowledgements",new AcknowledgeInput(item.Key))).StatusCode);
  Assert.Equal(first,Assert.Single((await Read(client)).Acknowledged));
  (await client.DeleteAsync(Endpoint+"/acknowledgements/"+first.Id)).EnsureSuccessStatusCode();
  var second=await Accept(client,item);Assert.NotEqual(first.Id,second.Id);
  Assert.Equal(HttpStatusCode.Conflict,(await client.DeleteAsync(Endpoint+"/acknowledgements/"+first.Id)).StatusCode);
  Assert.Equal(second,Assert.Single((await Read(client)).Acknowledged));
 }

 private static async Task<string> StoredState(GhostWatchDbContext db)
 {
  return JsonSerializer.Serialize(new{
   pools=await db.CapitalPools.AsNoTracking().ToListAsync(),tracks=await db.EconomyTracks.AsNoTracking().ToListAsync(),
   runs=await db.EconomicRuns.AsNoTracking().ToListAsync(),objectives=await db.Objectives.AsNoTracking().ToListAsync(),
   characters=await db.EveCharacters.AsNoTracking().ToListAsync(),facts=await db.EveSections.AsNoTracking().ToListAsync(),
   adjustments=await db.CapitalAdjustments.AsNoTracking().ToListAsync()});
 }
}
