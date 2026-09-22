using System.Text.Json;
using GhostWatch.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace GhostWatch.Api.Knowledge;
public sealed record KnowledgeLinks(Guid[] TrackIds, Guid[] RunIds, long[] CharacterIds, Guid[] PlaybookIds, Guid[] ObjectiveIds, Guid[] CapitalPoolIds)
{ public static KnowledgeLinks Empty => new([], [], [], [], [], []); }
public sealed record PlaybookInput(string? Name, string? Description, string? MarkdownBody, string[]? Tags, string? Status, KnowledgeLinks? Links, int Revision = 0);
public sealed record RecordInput(string? Title, string? RecordType, string? MarkdownBody, string[]? Tags, KnowledgeLinks? Links, int Revision = 0);
public static class KnowledgeEndpoints
{
    private static IResult Invalid(string message) => Results.Problem(statusCode: 400, title: message);
    private static IResult Conflict() => Results.Problem(statusCode: 409, title: "This document changed. Reload before saving.");
    public static void MapKnowledge(this WebApplication app)
    {
        var group = app.MapGroup("/api/knowledge");
        group.MapGet("/options", async (GhostWatchDbContext db, CancellationToken ct) =>
        {
            var tracks = await db.EconomyTracks.AsNoTracking().Select(x => new { x.Id, x.Name }).ToListAsync(ct);
            var runs = await db.EconomicRuns.AsNoTracking().Select(x => new { x.Id, x.Name }).ToListAsync(ct);
            var characters = await db.EveCharacters.AsNoTracking().Select(x => new { id = x.CharacterId, name = x.CharacterName }).ToListAsync(ct);
            var playbooks = await db.Playbooks.AsNoTracking().Select(x => new { x.Id, x.Name }).ToListAsync(ct);
            var objectives = await db.Objectives.AsNoTracking().Select(x => new { x.Id, x.Name }).ToListAsync(ct);
            var capitalPools = await db.CapitalPools.AsNoTracking().Select(x => new { x.Id, x.Name }).ToListAsync(ct);
            return Results.Ok(new { tracks, runs, characters, playbooks, objectives, capitalPools });
        });
        group.MapGet("/playbooks", async (GhostWatchDbContext db, CancellationToken ct) =>
            (await db.Playbooks.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct)).Select(x => new { x.Id, x.Name, x.Description, x.Status, x.UpdatedAt, x.Revision, tags = JsonSerializer.Deserialize<string[]>(x.TagsJson) }));
        group.MapGet("/playbooks/{id:guid}", async (Guid id, GhostWatchDbContext db, CancellationToken ct) =>
        {
            var row = await db.Playbooks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            return row is null ? Results.NotFound() : Results.Ok(new { row.Id, row.Name, row.Description, row.MarkdownBody, row.Status, row.CreatedAt, row.UpdatedAt, row.Revision,
                tags = JsonSerializer.Deserialize<string[]>(row.TagsJson), links = Links(await db.KnowledgeLinks.AsNoTracking().Where(x => x.PlaybookId == id).ToListAsync(ct)) });
        });
        group.MapGet("/playbooks/{id:guid}/revisions", async (Guid id, GhostWatchDbContext db, CancellationToken ct) =>
            await db.PlaybookRevisions.AsNoTracking().Where(x => x.PlaybookId == id).OrderByDescending(x => x.Version).ToListAsync(ct));
        group.MapPost("/playbooks", async (PlaybookInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (Validate(input.Name, input.MarkdownBody, input.Tags) is { } error) return Invalid(error);
            if (input.Status is not ("Draft" or "Active" or "Archived") || input.Description?.Length > 2000) return Invalid("Choose Draft, Active or Archived, and a description up to 2,000 characters.");
            if (await ValidateLinks(input.Links, true, db, ct) is { } linkError) return Invalid(linkError);
            var row = new Playbook(); Apply(row, input); db.Playbooks.Add(row);
            await SaveLinks(row.Id, null, input.Links, db, ct); await db.SaveChangesAsync(ct);
            return Results.Created($"/api/knowledge/playbooks/{row.Id}", new { row.Id, row.Revision });
        });
        group.MapPut("/playbooks/{id:guid}", async (Guid id, PlaybookInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (Validate(input.Name, input.MarkdownBody, input.Tags) is { } error) return Invalid(error);
            if (input.Status is not ("Draft" or "Active" or "Archived") || input.Description?.Length > 2000) return Invalid("Choose a valid status and description.");
            if (await ValidateLinks(input.Links, true, db, ct) is { } linkError) return Invalid(linkError);
            var row = await db.Playbooks.FindAsync([id], ct); if (row is null) return Results.NotFound();
            if (row.Revision != input.Revision) return Conflict();
            db.PlaybookRevisions.Add(new() { PlaybookId = id, Version = row.Revision, Name = row.Name, MarkdownBody = row.MarkdownBody, SavedAt = row.UpdatedAt });
            Apply(row, input); row.Revision++; await SaveLinks(id, null, input.Links, db, ct);
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return Conflict(); }
            catch (DbUpdateException failure) when (failure.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 }) { return Conflict(); }
            return Results.Ok(new { row.Id, row.Revision });
        });
        group.MapGet("/records", async (GhostWatchDbContext db, CancellationToken ct) =>
            (await db.EconomicRecords.AsNoTracking().OrderByDescending(x => x.UpdatedAt).ToListAsync(ct)).Select(x => new { x.Id, x.Title, x.RecordType, x.UpdatedAt, x.Revision, tags = JsonSerializer.Deserialize<string[]>(x.TagsJson) }));
        group.MapGet("/records/{id:guid}", async (Guid id, GhostWatchDbContext db, CancellationToken ct) =>
        {
            var row = await db.EconomicRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            return row is null ? Results.NotFound() : Results.Ok(new { row.Id, row.Title, row.RecordType, row.MarkdownBody, row.CreatedAt, row.UpdatedAt, row.Revision,
                tags = JsonSerializer.Deserialize<string[]>(row.TagsJson), links = Links(await db.KnowledgeLinks.AsNoTracking().Where(x => x.RecordId == id).ToListAsync(ct)) });
        });
        group.MapPost("/records", async (RecordInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (Validate(input.Title, input.MarkdownBody, input.Tags) is { } error) return Invalid(error);
            if (string.IsNullOrWhiteSpace(input.RecordType) || input.RecordType.Length > 80) return Invalid("Provide a record type up to 80 characters.");
            if (await ValidateLinks(input.Links, false, db, ct) is { } linkError) return Invalid(linkError);
            var row = new EconomicRecord(); Apply(row, input); db.EconomicRecords.Add(row);
            await SaveLinks(null, row.Id, input.Links, db, ct); await db.SaveChangesAsync(ct);
            return Results.Created($"/api/knowledge/records/{row.Id}", new { row.Id, row.Revision });
        });
        group.MapPut("/records/{id:guid}", async (Guid id, RecordInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (Validate(input.Title, input.MarkdownBody, input.Tags) is { } error) return Invalid(error);
            if (string.IsNullOrWhiteSpace(input.RecordType) || input.RecordType.Length > 80) return Invalid("Provide a record type up to 80 characters.");
            if (await ValidateLinks(input.Links, false, db, ct) is { } linkError) return Invalid(linkError);
            var row = await db.EconomicRecords.FindAsync([id], ct); if (row is null) return Results.NotFound();
            if (row.Revision != input.Revision) return Conflict();
            Apply(row, input); row.Revision++; await SaveLinks(null, id, input.Links, db, ct);
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return Conflict(); }
            catch (DbUpdateException failure) when (failure.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 }) { return Conflict(); }
            return Results.Ok(new { row.Id, row.Revision });
        });
    }
    private static KnowledgeLinks Links(List<KnowledgeLink> links) => new(
        links.Where(x => x.TrackId != null).Select(x => x.TrackId!.Value).ToArray(), links.Where(x => x.RunId != null).Select(x => x.RunId!.Value).ToArray(),
        links.Where(x => x.CharacterId != null).Select(x => x.CharacterId!.Value).ToArray(), links.Where(x => x.RelatedPlaybookId != null).Select(x => x.RelatedPlaybookId!.Value).ToArray(),
        links.Where(x => x.ObjectiveId != null).Select(x => x.ObjectiveId!.Value).ToArray(), links.Where(x => x.CapitalPoolId != null).Select(x => x.CapitalPoolId!.Value).ToArray());
    private static async Task SaveLinks(Guid? playbookId, Guid? recordId, KnowledgeLinks? input, GhostWatchDbContext db, CancellationToken ct)
    {
        db.KnowledgeLinks.RemoveRange(await db.KnowledgeLinks.Where(x => playbookId != null ? x.PlaybookId == playbookId : x.RecordId == recordId).ToListAsync(ct));
        var links = input ?? KnowledgeLinks.Empty;
        void Add(KnowledgeLink link) { link.PlaybookId = playbookId; link.RecordId = recordId; db.KnowledgeLinks.Add(link); }
        foreach (var id in links.TrackIds.Distinct()) Add(new() { TrackId = id });
        foreach (var id in links.RunIds.Distinct()) Add(new() { RunId = id });
        foreach (var id in links.CharacterIds.Distinct()) Add(new() { CharacterId = id });
        foreach (var id in links.PlaybookIds.Distinct()) Add(new() { RelatedPlaybookId = id });
        foreach (var id in links.ObjectiveIds.Distinct()) Add(new() { ObjectiveId = id });
        foreach (var id in links.CapitalPoolIds.Distinct()) Add(new() { CapitalPoolId = id });
    }
    private static string? Validate(string? name, string? body, string[]? tags) =>
        string.IsNullOrWhiteSpace(name) || name.Length > 160 || body?.Length > 100000 || tags?.Length > 30 || tags?.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 40) == true
            ? "Provide a title up to 160 characters, Markdown up to 100,000 characters and at most 30 tags of 1–40 characters." : null;
    private static async Task<string?> ValidateLinks(KnowledgeLinks? links, bool playbook, GhostWatchDbContext db, CancellationToken ct)
    {
        if (links is null) return null;
        if (links.TrackIds is null || links.RunIds is null || links.CharacterIds is null || links.PlaybookIds is null || links.ObjectiveIds is null || links.CapitalPoolIds is null) return "Link lists cannot be null.";
        if (links.TrackIds.Length + links.RunIds.Length + links.CharacterIds.Length + links.PlaybookIds.Length + links.ObjectiveIds.Length + links.CapitalPoolIds.Length > 200) return "Limit a document to 200 links.";
        if (playbook && (links.PlaybookIds.Length + links.ObjectiveIds.Length + links.CapitalPoolIds.Length > 0)) return "Playbooks link to Tracks, Runs and Characters.";
        if (await db.EconomyTracks.CountAsync(x => links.TrackIds.Contains(x.Id), ct) != links.TrackIds.Distinct().Count() ||
            await db.EconomicRuns.CountAsync(x => links.RunIds.Contains(x.Id), ct) != links.RunIds.Distinct().Count() ||
            await db.EveCharacters.CountAsync(x => links.CharacterIds.Contains(x.CharacterId), ct) != links.CharacterIds.Distinct().Count() ||
            await db.Playbooks.CountAsync(x => links.PlaybookIds.Contains(x.Id), ct) != links.PlaybookIds.Distinct().Count() ||
            await db.Objectives.CountAsync(x => links.ObjectiveIds.Contains(x.Id), ct) != links.ObjectiveIds.Distinct().Count() ||
            await db.CapitalPools.CountAsync(x => links.CapitalPoolIds.Contains(x.Id), ct) != links.CapitalPoolIds.Distinct().Count()) return "One or more linked records no longer exist.";
        return null;
    }
    private static void Apply(Playbook row, PlaybookInput input)
    { row.Name = input.Name!.Trim(); row.Description = input.Description ?? ""; row.MarkdownBody = input.MarkdownBody ?? ""; row.TagsJson = Tags(input.Tags); row.Status = input.Status!; row.UpdatedAt = DateTime.UtcNow; }
    private static void Apply(EconomicRecord row, RecordInput input)
    { row.Title = input.Title!.Trim(); row.RecordType = input.RecordType!.Trim(); row.MarkdownBody = input.MarkdownBody ?? ""; row.TagsJson = Tags(input.Tags); row.UpdatedAt = DateTime.UtcNow; }
    private static string Tags(string[]? tags) => JsonSerializer.Serialize((tags ?? []).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));
}
