using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Knowledge;
using GhostWatch.Api.Economics.Tracks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace GhostWatch.Tests;
public class KnowledgeTests
{
    [Fact]
    public async Task Markdown_revisions_and_flexible_linked_records_survive_edits()
    {
        await using var app = new TestApplication(); using var browser = app.CreateClient();
        Guid trackId;
        using(var scope=app.Services.CreateScope()) { var db=scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();var track=new EconomyTrack{Name="Industry"};db.EconomyTracks.Add(track);await db.SaveChangesAsync();trackId=track.Id; }
        var input=new PlaybookInput("Production", "Procedure", "# Original\n- [ ] Source materials", ["industry"], "Active", KnowledgeLinks.Empty with { TrackIds=[trackId] });
        var response=await browser.PostAsJsonAsync("/api/knowledge/playbooks",input);response.EnsureSuccessStatusCode();
        var id=(await response.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<Guid>();
        (await browser.PutAsJsonAsync($"/api/knowledge/playbooks/{id}",input with {MarkdownBody="# Revised",Revision=1})).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict,(await browser.PutAsJsonAsync($"/api/knowledge/playbooks/{id}",input with {Revision=1})).StatusCode);
        var history=(await browser.GetFromJsonAsync<JsonArray>($"/api/knowledge/playbooks/{id}/revisions"))!;
        Assert.Single(history);Assert.Equal(input.MarkdownBody,history[0]!["markdownBody"]!.GetValue<string>());Assert.NotNull(history[0]!["savedAt"]);
        var record=new RecordInput("Supplier finding","Custom sourcing observation","| Item | Source |\n|---|---|\n| Hull | Local |",["sourcing"],KnowledgeLinks.Empty with {TrackIds=[trackId],PlaybookIds=[id]});
        var created=await browser.PostAsJsonAsync("/api/knowledge/records",record);created.EnsureSuccessStatusCode();
        var recordId=(await created.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<Guid>();
        (await browser.PutAsJsonAsync($"/api/knowledge/records/{recordId}",record with {MarkdownBody="New finding",Revision=1})).EnsureSuccessStatusCode();
        var view=(await browser.GetFromJsonAsync<JsonObject>($"/api/knowledge/records/{recordId}"))!;
        Assert.Equal(id,view["links"]!["playbookIds"]![0]!.GetValue<Guid>());Assert.Equal("Custom sourcing observation",view["recordType"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest,(await browser.PostAsJsonAsync("/api/knowledge/records",record with {Links=KnowledgeLinks.Empty with {TrackIds=[Guid.NewGuid()]}})).StatusCode);
        using var verify=app.Services.CreateScope();var saved=verify.ServiceProvider.GetRequiredService<GhostWatchDbContext>();Assert.Equal(3,await saved.KnowledgeLinks.CountAsync());
    }
}
