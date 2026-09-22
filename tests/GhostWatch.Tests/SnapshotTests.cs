using System.Net.Http.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Economics.Capital;
using GhostWatch.Api.Economics.Snapshots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace GhostWatch.Tests;
public class SnapshotTests
{
 [Fact] public async Task Stored_values_are_immutable_and_monthly_capture_is_idempotent()
 {
  await using var app=new TestApplication();using var browser=app.CreateClient();
  using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();db.CapitalPools.Add(new CapitalPool{Name="Reserve",Role="Core Capital",AllocatedCapital=500});await db.SaveChangesAsync();}
  var response=await browser.PostAsJsonAsync("/api/economics/snapshots",new SnapshotInput("Before allocation","Remember the original amount"));response.EnsureSuccessStatusCode();var id=(await response.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<Guid>();
  string captured;
  using(var scope=app.Services.CreateScope()){
   var db=scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();captured=(await db.EconomicSnapshots.FindAsync(id))!.ValuesJson;var pool=await db.CapitalPools.SingleAsync();pool.AllocatedCapital=2000;await db.SaveChangesAsync();
   var store=scope.ServiceProvider.GetRequiredService<SnapshotStore>();var now=new DateTime(2026,9,28,1,0,0,DateTimeKind.Utc);var first=await store.Capture(now,true,null,null,default);var again=await store.Capture(now.AddDays(1),true,null,null,default);Assert.Equal(first.Id,again.Id);Assert.Equal("2026-09",first.MonthKey);Assert.Equal(2000,SnapshotStore.Values(first).Metrics["coreCapital"]);
   var next=await store.Capture(now.AddMonths(1),true,null,null,default);Assert.NotEqual(first.Id,next.Id);Assert.Equal(3,await db.EconomicSnapshots.CountAsync());Assert.Equal(captured,(await db.EconomicSnapshots.FindAsync(id))!.ValuesJson);
  }
  var original=(await browser.GetFromJsonAsync<JsonObject>($"/api/economics/snapshots/{id}"))!;Assert.Equal(500,original["values"]!["metrics"]!["coreCapital"]!.GetValue<decimal>());Assert.Equal("Remember the original amount",original["note"]!.GetValue<string>());
 }
 [Fact] public async Task Background_worker_captures_current_month_at_startup()
 {
  await using var app=new TestApplication(builder=>builder.UseSetting("Snapshots:AutomaticEnabled","true"));using var browser=app.CreateClient();
  JsonArray? rows=null;for(var i=0;i<50;i++){rows=await browser.GetFromJsonAsync<JsonArray>("/api/economics/snapshots");if(rows!.Count>0)break;await Task.Delay(50);}
  Assert.Single(rows!);Assert.Equal(DateTime.UtcNow.ToString("yyyy-MM"),rows![0]!["monthKey"]!.GetValue<string>());Assert.Equal("AutomaticMonthly",rows[0]!["trigger"]!.GetValue<string>());
 }
}
