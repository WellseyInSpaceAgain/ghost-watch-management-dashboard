using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Economics.Capital;
using GhostWatch.Api.Economics.Charts;
using GhostWatch.Api.Economics.Runs;
using GhostWatch.Api.Economics.Tracks;
using GhostWatch.Api.Economics.Snapshots;
using Microsoft.Extensions.DependencyInjection;
namespace GhostWatch.Tests;
public class ChartTests
{
 private static string Config(string source="runs",string field="actualProfit",string aggregation="sum",string type="bar",string dimension="productName",ChartFilters? filters=null)=>JsonSerializer.Serialize(new ChartConfig("Test chart",null,type,source,filters,new(dimension,"Group"),[new(field,"Value",aggregation,"isk")]),ChartValidation.JsonOptions);
 [Theory]
 [InlineData("{\"sql\":\"SELECT * FROM EveCharacters\"}")]
 [InlineData("{\"title\":\"unsafe\",\"type\":\"bar\",\"dataSource\":\"runs\",\"x\":{\"field\":\"productName\"},\"series\":[{\"field\":\"actualProfit\",\"label\":\"profit\",\"aggregation\":\"sum\",\"format\":\"isk\"}],\"javascript\":\"alert(1)\"}")]
 [InlineData("null")]
 [InlineData("{\"title\":\"Missing configuration\"}")]
 public void Arbitrary_or_incomplete_configs_are_rejected(string json)=>Assert.Throws<ChartValidationException>(()=>ChartValidation.Parse(json));
 [Fact] public async Task Queries_aggregate_known_values_preserve_unknowns_and_resolve_track_context()
 {
  await using var app=new TestApplication();using var browser=app.CreateClient();Guid trackId;
  using(var scope=app.Services.CreateScope()){
   var db=scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();var track=new EconomyTrack{Name="Workshop"};trackId=track.Id;db.EconomyTracks.Add(track);
   db.EconomicRuns.AddRange(new EconomicRun{Name="First",TrackId=trackId,ProductName="Shield",ActualInputCost=50,ActualJobCost=0,ActualOtherCost=0,ActualRevenue=100,StartedAt=DateTime.UtcNow.AddDays(-2)},new EconomicRun{Name="Second",TrackId=trackId,ProductName="Shield",ActualInputCost=10,ActualJobCost=0,ActualOtherCost=0,ActualRevenue=40,StartedAt=DateTime.UtcNow.AddDays(-1)},new EconomicRun{Name="Unknown",TrackId=trackId,ProductName="Hull"});await db.SaveChangesAsync();
  }
  foreach(var (aggregation,expected) in new[]{("sum",80m),("average",40m),("min",30m),("max",50m),("count",2m),("latest",30m)}){
   var response=await browser.PostAsJsonAsync("/api/economics/charts/preview",new PreviewInput(Config(aggregation:aggregation,filters:new(Product:"Shield"))));response.EnsureSuccessStatusCode();var data=(await response.Content.ReadFromJsonAsync<ChartData>())!;Assert.Equal(expected,data.Series[0].Values[0]);Assert.Equal("Shield",data.Labels[0]);
  }
  var unknown=await browser.PostAsJsonAsync("/api/economics/charts/preview",new PreviewInput(Config(dimension:"all")));Assert.Null((await unknown.Content.ReadFromJsonAsync<ChartData>())!.Series[0].Values[0]);
  var scoped=Config(filters:new(TrackId:"CURRENT_TRACK"));Assert.Equal(HttpStatusCode.BadRequest,(await browser.PostAsJsonAsync("/api/economics/charts/preview",new PreviewInput(scoped))).StatusCode);
  (await browser.PostAsJsonAsync("/api/economics/charts/preview",new PreviewInput(scoped,trackId))).EnsureSuccessStatusCode();
  Assert.Throws<ChartValidationException>(()=>ChartValidation.Parse(Config(field:"ProtectedRefreshToken")));
  Assert.Throws<ChartValidationException>(()=>ChartValidation.Parse(Config(source:"arbitraryTable")));
  var malformed=await browser.PostAsJsonAsync("/api/economics/charts/preview",new PreviewInput("{"));Assert.Equal(HttpStatusCode.BadRequest,malformed.StatusCode);Assert.Contains("Invalid chart JSON",await malformed.Content.ReadAsStringAsync());
  Assert.Throws<ChartValidationException>(()=>ChartValidation.Parse(Config(source:"tracks",field:"profit30d",dimension:"name",filters:new(Product:"Shield"))));
 }
 [Fact] public async Task Historical_charts_use_stored_values_and_shared_placements_keep_independent_layouts()
 {
  await using var app=new TestApplication();using var browser=app.CreateClient();Guid trackId;
  using(var scope=app.Services.CreateScope()){
   var db=scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();var pool=new CapitalPool{Name="Reserve",Role="Core Capital",AllocatedCapital=600};var track=new EconomyTrack{Name="Industry"};trackId=track.Id;db.CapitalPools.Add(pool);db.EconomyTracks.Add(track);await db.SaveChangesAsync();
   await scope.ServiceProvider.GetRequiredService<SnapshotStore>().Capture(DateTime.UtcNow,false,"Original",null,default);pool.AllocatedCapital=999;await db.SaveChangesAsync();
  }
  var json=Config(source:"snapshots",field:"coreCapital",aggregation:"latest",type:"line",dimension:"date");
  var preview=await browser.PostAsJsonAsync("/api/economics/charts/preview",new PreviewInput(json));preview.EnsureSuccessStatusCode();Assert.Equal(600,(await preview.Content.ReadFromJsonAsync<ChartData>())!.Series[0].Values[0]);
  var created=await browser.PostAsJsonAsync("/api/economics/charts/definitions",new ChartInput("Historical capital",json));created.EnsureSuccessStatusCode();var definition=(await created.Content.ReadFromJsonAsync<ChartDefinition>())!;
  async Task<ChartPlacement> Place(string type,Guid? id,string width){var result=await browser.PostAsJsonAsync("/api/economics/charts/placements",new PlacementInput(definition.Id,type,id,width));result.EnsureSuccessStatusCode();return (await result.Content.ReadFromJsonAsync<ChartPlacement>())!;}
  var dashboard=await Place("Dashboard",null,"Wide");var trackPlacement=await Place("Track",trackId,"Small");var second=await Place("Dashboard",null,"Medium");
  Assert.Equal(HttpStatusCode.Conflict,(await browser.DeleteAsync($"/api/economics/charts/definitions/{definition.Id}?revision=1")).StatusCode);
  var layout=new LayoutInput("Dashboard",null,[new(second.Id,"Small",1),new(dashboard.Id,"Medium",1)]);
  (await browser.PutAsJsonAsync("/api/economics/charts/layout",layout)).EnsureSuccessStatusCode();
  Assert.Equal(HttpStatusCode.Conflict,(await browser.PutAsJsonAsync("/api/economics/charts/layout",layout)).StatusCode);
  var placements=(await browser.GetFromJsonAsync<ChartPlacement[]>("/api/economics/charts/placements?pageType=Dashboard"))!;Assert.Equal(second.Id,placements[0].Id);Assert.Equal("Medium",placements[1].Width);
  (await browser.DeleteAsync($"/api/economics/charts/placements/{trackPlacement.Id}?revision=1")).EnsureSuccessStatusCode();
  Assert.Single((await browser.GetFromJsonAsync<JsonArray>("/api/economics/charts/definitions"))!);
  (await browser.GetAsync($"/api/economics/charts/placements/{dashboard.Id}/data")).EnsureSuccessStatusCode();
 }
}
