using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace GhostWatch.Api.Economics.Runs;

public sealed record JobReference(long CharacterId, long JobId);
public sealed record RunInput(EconomicRun Run, JobReference? Job = null);
public static class RunEndpoints
{
    public static readonly string[] Types = ["Manufacturing", "Invention", "Research", "Reaction", "Trading", "PI", "R&D", "Strategic Supply", "Loot", "Other"];
    public static readonly string[] Purposes = ["Commercial", "R&D", "Strategic Supply", "Internal Consumption", "Other"];
    public static readonly string[] Statuses = ["Planning", "Active", "Completed", "Selling", "Evaluated", "Cancelled"];
    public static readonly string[] Verdicts = ["Scale", "Repeat", "Retest", "Drop", "R&D Successful", "R&D Failed", "Internal Supply", "No Verdict"];
    private static IResult Invalid(string message) => Results.Problem(statusCode: 400, title: message);
    private static IResult Conflict(string message = "This Run changed. Reload before saving.") => Results.Problem(statusCode: 409, title: message);
    public static void MapRuns(this WebApplication app)
    {
        var group = app.MapGroup("/api/economics/runs");
        group.MapGet("/options", () => new { types = Types, purposes = Purposes, statuses = Statuses, verdicts = Verdicts });
        group.MapGet("/", async (Guid? trackId, GhostWatchDbContext db, CancellationToken ct) =>
        {
            var rows = await db.EconomicRuns.AsNoTracking().Include(x => x.Track).Include(x => x.CapitalPool).Where(x => trackId == null || x.TrackId == trackId).OrderByDescending(x => x.StartedAt).ToListAsync(ct);
            return Results.Ok(rows.Select(View));
        });
        group.MapGet("/{id:guid}", async (Guid id, GhostWatchDbContext db, CancellationToken ct) =>
        {
            var run = await db.EconomicRuns.AsNoTracking().Include(x => x.Track).Include(x => x.CapitalPool).SingleOrDefaultAsync(x => x.Id == id, ct);
            return run is null ? Results.NotFound() : Results.Ok(View(run));
        });
        group.MapPost("/", async (RunInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (await Validate(input.Run, db, ct) is { } error) return Invalid(error);
            if (input.Job is { } job)
            {
                if (!await db.EveIndustryJobs.AnyAsync(x => x.CharacterId == job.CharacterId && x.JobId == job.JobId, ct)) return Invalid("The ESI job was not found.");
                if (await db.RunJobs.AnyAsync(x => x.CharacterId == job.CharacterId && x.JobId == job.JobId, ct)) return Conflict("This job is already associated with a Run.");
            }
            var run = input.Run; run.Id = Guid.NewGuid(); run.Revision = 1; run.CreatedAt = DateTime.UtcNow; Normalise(run);
            db.EconomicRuns.Add(run);
            if (input.Job is { } link) db.RunJobs.Add(new() { CharacterId = link.CharacterId, JobId = link.JobId, RunId = run.Id });
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Conflict("The job association changed. Reload and retry."); }
            return Results.Created($"/api/economics/runs/{run.Id}", new { run.Id });
        });
        group.MapPut("/{id:guid}", async (Guid id, RunInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (await Validate(input.Run, db, ct) is { } error) return Invalid(error);
            var old = await db.EconomicRuns.FindAsync([id], ct);
            if (old is null) return Results.NotFound();
            if (old.Revision != input.Run.Revision) return Conflict();
            var run = input.Run; run.Id = id; run.CreatedAt = old.CreatedAt; run.Revision++; Normalise(run);
            db.Entry(old).CurrentValues.SetValues(run);
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return Conflict(); }
            return Results.Ok(new { old.Id, old.Revision });
        });
        group.MapPost("/{id:guid}/jobs", async (Guid id, JobReference job, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (!await db.EconomicRuns.AnyAsync(x => x.Id == id, ct)) return Results.NotFound();
            if (!await db.EveIndustryJobs.AnyAsync(x => x.CharacterId == job.CharacterId && x.JobId == job.JobId, ct)) return Invalid("The ESI job was not found.");
            if (await db.RunJobs.AnyAsync(x => x.CharacterId == job.CharacterId && x.JobId == job.JobId, ct)) return Conflict("This job is already associated. Remove its association first.");
            db.RunJobs.Add(new() { CharacterId = job.CharacterId, JobId = job.JobId, RunId = id });
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Conflict("This job was associated in another operation."); }
            return Results.NoContent();
        });
        group.MapDelete("/{id:guid}/jobs/{characterId:long}/{jobId:long}", async (Guid id, long characterId, long jobId, GhostWatchDbContext db, CancellationToken ct) =>
        {
            var link = await db.RunJobs.SingleOrDefaultAsync(x => x.RunId == id && x.CharacterId == characterId && x.JobId == jobId, ct);
            if (link is null) return Results.NotFound();
            db.RunJobs.Remove(link); await db.SaveChangesAsync(ct); return Results.NoContent();
        });
        app.MapGet("/api/eve/industry-jobs", Jobs);
    }
    public static async Task<IResult> Jobs(Guid? runId, bool? unassociated, Guid? trackId, GhostWatchDbContext db, CancellationToken ct)
    {
        var jobs = await db.EveIndustryJobs.AsNoTracking().OrderByDescending(x => x.StartDate).ToListAsync(ct);
        var links = await db.RunJobs.AsNoTracking().Include(x => x.Run).ThenInclude(x => x.Track).ToListAsync(ct);
        var characters = await db.EveCharacters.AsNoTracking().ToDictionaryAsync(x => x.CharacterId, x => x.CharacterName, ct);
        var types = await db.PublicEveLookups.AsNoTracking().Where(x => x.Key.StartsWith("type:")).ToDictionaryAsync(x => x.Key, ct);
        var trackCharacters = trackId is null ? [] : await db.CharacterTracks.Where(x => x.TrackId == trackId).Select(x => x.CharacterId).ToListAsync(ct);
        var result = jobs.Select(job =>
        {
            var link = links.Find(x => x.CharacterId == job.CharacterId && x.JobId == job.JobId);
            var type = job.ProductTypeId ?? job.BlueprintTypeId;
            var product = types.TryGetValue($"type:{type}", out var row) ? JsonNode.Parse(row.Json)?["name"]?.GetValue<string>() : null;
            return new { job.CharacterId, characterName = characters[job.CharacterId], job.JobId, job.ActivityId, job.ProductTypeId, job.BlueprintTypeId,
                productName = product ?? $"Type {type} (name unavailable)", job.Runs, job.Status, job.StartDate, job.EndDate, job.LastSeenAt,
                runId = link?.RunId, runName = link?.Run.Name, trackId = link?.Run.TrackId, trackName = link?.Run.Track.Name };
        }).Where(x => (runId is null || x.runId == runId) && (unassociated != true || x.runId is null) && (trackId == null || x.trackId == trackId || x.runId == null && trackCharacters.Contains(x.CharacterId)));
        return Results.Ok(result);
    }
    private static object View(EconomicRun run) => new { run, trackName = run.Track.Name, capitalPoolName = run.CapitalPool?.Name, financials = RunMetrics.Calculate(run) };
    private static void Normalise(EconomicRun run)
    {
        run.Notes ??= "";
        run.Name = run.Name.Trim(); run.ProductName = string.IsNullOrWhiteSpace(run.ProductName) ? null : run.ProductName.Trim();
        run.UpdatedAt = DateTime.UtcNow; run.StartedAt = run.StartedAt.ToUniversalTime(); run.CompletedAt = run.CompletedAt?.ToUniversalTime();
    }
    private static async Task<string?> Validate(EconomicRun? run, GhostWatchDbContext db, CancellationToken ct)
    {
        if (run is null || string.IsNullOrWhiteSpace(run.Name) || run.Name.Length > 120 || run.Notes?.Length > 20000 || run.ProductName?.Length > 200) return "Enter a Run name up to 120 characters, product name up to 200 and notes up to 20,000.";
        if (!Types.Contains(run.RunType) || !Purposes.Contains(run.Purpose) || !Statuses.Contains(run.Status) || !Verdicts.Contains(run.Verdict)) return "Choose supported Run type, purpose, status and verdict.";
        if (!await db.EconomyTracks.AnyAsync(x => x.Id == run.TrackId, ct)) return "Choose an existing Track.";
        if (run.PlaybookId is { } book && !await db.Playbooks.AnyAsync(x => x.Id == book, ct)) return "Choose an existing Playbook.";
        if (run.CapitalPoolId is { } pool && !await db.CapitalPools.AnyAsync(x => x.Id == pool, ct)) return "Choose an existing Capital Pool.";
        if (new[] { run.Quantity, run.ExpectedInputCost, run.ExpectedJobCost, run.ExpectedOtherCost, run.ExpectedRevenue, run.ActualInputCost, run.ActualJobCost, run.ActualOtherCost, run.ActualRevenue, run.CapitalTiedUp, run.ManufacturingHours, run.TimeToSellDays }.Any(x => x is < 0 or > 1000000000000000m) || run.ConcurrentSlots is < 1 or > 1000 || run.ProductTypeId is <= 0) return "Amounts and durations must be nonnegative, with positive type IDs and slot counts.";
        if (run.StartedAt == default || run.CompletedAt < run.StartedAt) return "Completion cannot precede the start date.";
        if (RunMetrics.Realised(run) && run.CompletedAt is null) return "Completed/evaluated Runs need a completion date. Financial results can remain unknown.";
        return null;
    }
}
