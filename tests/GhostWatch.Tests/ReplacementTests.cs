using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Economics.Replacement;
using GhostWatch.Api.Economics.Capital;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace GhostWatch.Tests;
public class ReplacementTests
{
 [Fact] public async Task Coverage_is_manual_and_default_changes_atomically_without_erasing_packages()
 {
  await using var app=new TestApplication();using var browser=app.CreateClient();
  var input=new ReplacementInput("Standard T3C","Doctrine",800000000,true,"Manual estimate");
  var created=await browser.PostAsJsonAsync("/api/economics/replacement-packages",input);created.EnsureSuccessStatusCode();
  var id=(await created.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<Guid>();
  var unknown=(await browser.GetFromJsonAsync<JsonObject>("/api/economics/replacement-packages"))!;Assert.Null(unknown["coverage"]![id.ToString()]);
  using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();db.CapitalPools.Add(new CapitalPool{Name="Doctrine fund",Role="Ghost Watch Treasury",AllocatedCapital=2400000000});await db.SaveChangesAsync();}
  var known=(await browser.GetFromJsonAsync<JsonObject>("/api/economics/replacement-packages"))!;Assert.Equal(3,known["coverage"]![id.ToString()]!.GetValue<decimal>());
  (await browser.PostAsJsonAsync("/api/economics/replacement-packages",input with {Name="Faction T3C",EstimatedReplacementValue=1200000000})).EnsureSuccessStatusCode();
  using var verify=app.Services.CreateScope();var saved=verify.ServiceProvider.GetRequiredService<GhostWatchDbContext>();Assert.Equal(2,await saved.ReplacementPackages.CountAsync());Assert.Single(await saved.ReplacementPackages.Where(x=>x.IsDefault).ToListAsync());Assert.False((await saved.ReplacementPackages.FindAsync(id))!.IsDefault);
  Assert.Equal(HttpStatusCode.Conflict,(await browser.PutAsJsonAsync($"/api/economics/replacement-packages/{id}",input with {Revision=1})).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await browser.PostAsJsonAsync("/api/economics/replacement-packages",input with {EstimatedReplacementValue=0})).StatusCode);
 }
}
