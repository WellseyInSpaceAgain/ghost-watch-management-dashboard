using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Economics.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace GhostWatch.Tests;
public class PlanningTests
{
    [Fact]
    public async Task Gates_and_checklists_preserve_manual_progress_and_completion_history()
    {
        await using var app = new TestApplication(); using var browser = app.CreateClient();
        var input = new ObjectiveInput("Activate another account", "Manual readiness gate", null, "Gate", "Active", null, 25, [new("Demonstrated demand", false), new("Capital available", true)], "User decision");
        var response = await browser.PostAsJsonAsync("/api/economics/objectives", input); response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<Guid>();
        var view = (await browser.GetFromJsonAsync<JsonObject>($"/api/economics/objectives/{id}"))!;
        Assert.Equal(1, view["completedConditions"]!.GetValue<int>()); Assert.Equal(25, view["manualProgress"]!.GetValue<decimal>());
        Assert.Equal(HttpStatusCode.Conflict, (await browser.PutAsJsonAsync($"/api/economics/objectives/{id}", input)).StatusCode);
        (await browser.PutAsJsonAsync($"/api/economics/objectives/{id}", input with { Revision = 1, Status = "Completed", ManualProgress = 100, Conditions = [new("Demonstrated demand", true), new("Capital available", true)] })).EnsureSuccessStatusCode();
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
        var saved = await db.Objectives.SingleAsync(); Assert.NotNull(saved.CompletedAt); Assert.Equal("User decision", saved.Notes); Assert.Equal("Gate", saved.Type);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsJsonAsync("/api/economics/objectives", input with { ManualProgress = 101 })).StatusCode);
    }
    [Fact]
    public async Task Production_stages_are_manual_and_empty_or_duplicate_sets_are_handled_safely()
    {
        await using var app = new TestApplication(); using var browser = app.CreateClient(); Guid id;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>(); var track = new GhostWatch.Api.Economics.Tracks.EconomyTrack { Name = "Tengu" }; id = track.Id; db.EconomyTracks.Add(track); await db.SaveChangesAsync();
        }
        Assert.Null(PlanningEndpoints.Internalisation([]));
        var input = new StrategyInput([new("Reactions", true), new("Hull assembly", false), new("Sourcing", false)]);
        (await browser.PutAsJsonAsync($"/api/economics/tracks/{id}/strategy", input)).EnsureSuccessStatusCode();
        var saved = (await browser.GetFromJsonAsync<JsonObject>($"/api/economics/tracks/{id}/strategy"))!;
        Assert.Equal(100m / 3, saved["internalisation"]!.GetValue<decimal>());
        Assert.Equal(3, saved["stages"]!.AsArray().Count);
        Assert.Equal(HttpStatusCode.Conflict, (await browser.PutAsJsonAsync($"/api/economics/tracks/{id}/strategy", input)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PutAsJsonAsync($"/api/economics/tracks/{id}/strategy", new StrategyInput([new("Hull", true), new(" hull ", false)], 1))).StatusCode);
    }
}
