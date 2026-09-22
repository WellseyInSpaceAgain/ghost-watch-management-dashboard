using System.Net;
using System.Net.Http.Json;
using GhostWatch.Api.Economics.Tracks;

namespace GhostWatch.Tests;

public class TrackTests
{
    private static TrackInput Input(string name = "T2 Workshop", string status = "Planning", int? revision = null) =>
        new(name, "Repeatable production", status, "Cashflow", "Keep the first batch small.", revision);

    [Fact]
    public async Task Create_edit_archive_and_restore_preserves_track_identity_and_notes()
    {
        await using var app = new TestApplication();
        using var client = app.CreateClient();
        var create = await client.PostAsJsonAsync("/api/economics/tracks", Input());
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var track = (await create.Content.ReadFromJsonAsync<EconomyTrack>())!;
        Assert.NotEqual(Guid.Empty, track.Id);
        Assert.Equal(1, track.Revision);
        var archived = await client.PutAsJsonAsync(create.Headers.Location, Input(status: "Archived", revision: 1));
        archived.EnsureSuccessStatusCode();
        var saved = (await archived.Content.ReadFromJsonAsync<EconomyTrack>())!;
        Assert.NotNull(saved.ArchivedAt);
        Assert.Equal(track.CreatedAt, saved.CreatedAt);
        Assert.Empty((await client.GetFromJsonAsync<EconomyTrack[]>("/api/economics/tracks"))!);
        Assert.Single((await client.GetFromJsonAsync<EconomyTrack[]>("/api/economics/tracks?includeArchived=true"))!);
        var detail = (await client.GetFromJsonAsync<EconomyTrack>(create.Headers.Location))!;
        Assert.Equal(track.Notes, detail.Notes);
        var restored = await client.PutAsJsonAsync(create.Headers.Location, Input(status: "Active", revision: saved.Revision));
        restored.EnsureSuccessStatusCode();
        var active = (await restored.Content.ReadFromJsonAsync<EconomyTrack>())!;
        Assert.Equal(track.Id, active.Id);
        Assert.Null(active.ArchivedAt);
        Assert.Single((await client.GetFromJsonAsync<EconomyTrack[]>("/api/economics/tracks"))!);
    }

    [Fact]
    public async Task Stale_editor_cannot_overwrite_newer_management_notes()
    {
        await using var app = new TestApplication();
        using var client = app.CreateClient();
        var created = await client.PostAsJsonAsync("/api/economics/tracks", Input());
        var saved = await client.PutAsJsonAsync(created.Headers.Location, Input(revision: 1) with { Notes = "Latest finding" });
        saved.EnsureSuccessStatusCode();
        var stale = await client.PutAsJsonAsync(created.Headers.Location, Input(revision: 1));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("Latest finding", (await client.GetFromJsonAsync<EconomyTrack>(created.Headers.Location))!.Notes);
    }

    [Theory]
    [InlineData("", "Planning", "Cashflow")]
    [InlineData("   ", "Planning", "Cashflow")]
    [InlineData("Track", "Invalid", "Cashflow")]
    [InlineData("Track", "Active", "Invalid")]
    public async Task Invalid_tracks_are_rejected_without_creating_rows(string name, string status, string purpose)
    {
        await using var app = new TestApplication();
        using var client = app.CreateClient();
        var result = await client.PostAsJsonAsync("/api/economics/tracks", Input(name, status) with { Purpose = purpose });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("errors", await result.Content.ReadAsStringAsync());
        Assert.Empty((await client.GetFromJsonAsync<EconomyTrack[]>("/api/economics/tracks?includeArchived=true"))!);
    }

    [Fact]
    public async Task Long_notes_and_names_are_rejected_and_unknown_ids_return_not_found()
    {
        await using var app = new TestApplication();
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/economics/tracks", Input(new string('x', 121)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/economics/tracks", Input() with { Notes = new string('x', 20001) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/economics/tracks/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/economics/tracks/{Guid.NewGuid()}", Input(revision: 1))).StatusCode);
    }
}
