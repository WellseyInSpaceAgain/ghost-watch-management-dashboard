using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Management;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GhostWatch.Tests;

public class CharacterManagementTests
{
    [Fact]
    public async Task Accounts_assignments_and_multiple_track_links_persist_with_revision_checks()
    {
        await using var app = new TestApplication();
        using var browser = app.CreateClient();
        Guid first, second;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
            db.EveCharacters.Add(new() { CharacterId = 7, CharacterName = "Planner", RefreshToken = "untouched-token" });
            var tracks = new[] { new GhostWatch.Api.Economics.Tracks.EconomyTrack { Name = "Research" }, new GhostWatch.Api.Economics.Tracks.EconomyTrack { Name = "Supply" } };
            db.EconomyTracks.AddRange(tracks); first = tracks[0].Id; second = tracks[1].Id;
            await db.SaveChangesAsync();
        }
        var created = await browser.PostAsJsonAsync("/api/management/accounts", new AccountInput("Main account", "Omega", "Manual subscription"));
        created.EnsureSuccessStatusCode();
        var account = (await created.Content.ReadFromJsonAsync<ManagedAccount>())!;
        var input = new CharacterPlanInput(account.Id, "Economic controller", "Local decision", [first, second]);
        (await browser.PutAsJsonAsync("/api/management/characters/7", input)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await browser.PutAsJsonAsync("/api/management/characters/7", input)).StatusCode);
        var plan = (await browser.GetFromJsonAsync<JsonObject>("/api/management/characters/7"))!;
        Assert.Equal("Omega", plan["subscription"]!.GetValue<string>());
        Assert.Equal(2, plan["tracks"]!.AsArray().Count);
        var linked = (await browser.GetFromJsonAsync<JsonArray>($"/api/management/tracks/{first}/characters"))!;
        Assert.Equal("Planner", linked[0]!["characterName"]!.GetValue<string>());
        (await browser.PutAsJsonAsync($"/api/management/accounts/{account.Id}", new AccountInput("Main renamed", "Alpha", "Changed manually", account.Revision))).EnsureSuccessStatusCode();
        var list = (await browser.GetFromJsonAsync<JsonArray>("/api/eve/characters"))!;
        Assert.Equal("Main renamed", list[0]!["accountName"]!.GetValue<string>());
        Assert.Equal("Alpha", list[0]!["subscription"]!.GetValue<string>());
        (await browser.PutAsJsonAsync("/api/management/characters/7", input with { AccountId = null, TrackIds = [second], Revision = 1 })).EnsureSuccessStatusCode();
        using var check = app.Services.CreateScope();
        var saved = check.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
        Assert.Single(await saved.CharacterTracks.ToListAsync());
        Assert.Null((await saved.CharacterPlans.SingleAsync()).AccountId);
        Assert.Equal("untouched-token", (await saved.EveCharacters.SingleAsync()).RefreshToken);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PutAsJsonAsync("/api/management/characters/7", input with { AccountId = Guid.NewGuid(), Revision = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsJsonAsync("/api/management/accounts", new AccountInput("", "Invalid", ""))).StatusCode);
    }
}
