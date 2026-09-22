using System.Text.Json;
using GhostWatch.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace GhostWatch.Api.Economics.Planning;
public sealed record ObjectiveInput(string? Name, string? Description, Guid? TrackId, string? Type, string? Status, DateTime? TargetDate, decimal? ManualProgress, ChecklistItem[]? Conditions, string? Notes, int Revision = 0);
public sealed record StrategyInput(ProductionStage[]? Stages, int Revision = 0);
public static class PlanningEndpoints
{
    public static readonly string[] Statuses = ["Planning", "Active", "Completed", "Cancelled"];
    private static IResult Invalid(string message) => Results.Problem(statusCode: 400, title: message);
    private static IResult Conflict() => Results.Problem(statusCode: 409, title: "Planning data changed. Reload before saving.");
    public static void MapPlanning(this WebApplication app)
    {
        var group = app.MapGroup("/api/economics/objectives");
        group.MapGet("/", async (Guid? trackId, GhostWatchDbContext db, CancellationToken ct) =>
            (await db.Objectives.AsNoTracking().Include(x => x.Track).Where(x => trackId == null || x.TrackId == trackId).OrderBy(x => x.Name).ToListAsync(ct)).Select(View));
        group.MapGet("/{id:guid}", async (Guid id, GhostWatchDbContext db, CancellationToken ct) =>
            await db.Objectives.AsNoTracking().Include(x => x.Track).SingleOrDefaultAsync(x => x.Id == id, ct) is { } objective ? Results.Ok(View(objective)) : Results.NotFound());
        group.MapPost("/", async (ObjectiveInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (await Validate(input, db, ct) is { } error) return Invalid(error);
            var objective = new Objective(); Apply(objective, input); db.Objectives.Add(objective); await db.SaveChangesAsync(ct);
            return Results.Created($"/api/economics/objectives/{objective.Id}", new { objective.Id });
        });
        group.MapPut("/{id:guid}", async (Guid id, ObjectiveInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (await Validate(input, db, ct) is { } error) return Invalid(error);
            var objective = await db.Objectives.FindAsync([id], ct); if (objective is null) return Results.NotFound();
            if (objective.Revision != input.Revision) return Conflict();
            Apply(objective, input); objective.Revision++;
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return Conflict(); }
            return Results.Ok(new { objective.Id, objective.Revision });
        });
        app.MapGet("/api/economics/tracks/{id:guid}/strategy", async (Guid id, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (!await db.EconomyTracks.AnyAsync(x => x.Id == id, ct)) return Results.NotFound();
            var strategy = await db.TrackStrategies.AsNoTracking().SingleOrDefaultAsync(x => x.TrackId == id, ct);
            var stages = JsonSerializer.Deserialize<ProductionStage[]>(strategy?.StagesJson ?? "[]")!;
            return Results.Ok(new { stages, revision = strategy?.Revision ?? 0, internalisation = Internalisation(stages) });
        });
        app.MapPut("/api/economics/tracks/{id:guid}/strategy", async (Guid id, StrategyInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (!await db.EconomyTracks.AnyAsync(x => x.Id == id, ct)) return Results.NotFound();
            if (input.Stages is null || input.Stages.Length > 100 || input.Stages.Any(x => x is null || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 120) || input.Stages.Select(x => x.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != input.Stages.Length)
                return Invalid("Provide at most 100 uniquely named stages, each up to 120 characters.");
            var strategy = await db.TrackStrategies.FindAsync([id], ct);
            if ((strategy?.Revision ?? 0) != input.Revision) return Conflict();
            if (strategy is null) { strategy = new() { TrackId = id, Revision = 0 }; db.TrackStrategies.Add(strategy); }
            strategy.StagesJson = JsonSerializer.Serialize(input.Stages.Select(x => x with { Name = x.Name.Trim() })); strategy.Revision++;
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return Conflict(); }
            return Results.Ok(new { strategy.Revision, internalisation = Internalisation(input.Stages) });
        });
    }
    public static decimal? Internalisation(IEnumerable<ProductionStage> stages)
    { var rows = stages.ToArray(); return rows.Length == 0 ? null : 100m * rows.Count(x => x.Internal) / rows.Length; }
    public static object View(Objective objective)
    {
        var conditions = JsonSerializer.Deserialize<ChecklistItem[]>(objective.ConditionsJson)!;
        return new { objective.Id, objective.Name, objective.Description, objective.TrackId, trackName = objective.Track?.Name, objective.Type,
            objective.Status, objective.TargetDate, objective.ManualProgress, conditions, completedConditions = conditions.Count(x => x.Done),
            objective.Notes, objective.CreatedAt, objective.UpdatedAt, objective.CompletedAt, objective.Revision };
    }
    private static async Task<string?> Validate(ObjectiveInput input, GhostWatchDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 120 || input.Description?.Length > 2000 || input.Notes?.Length > 20000) return "Enter a name up to 120 characters, description up to 2,000 and notes up to 20,000.";
        if (input.Type is not ("Objective" or "Gate") || input.Status is null || !Statuses.Contains(input.Status) || input.ManualProgress is < 0 or > 100) return "Choose a valid type/status and progress between 0 and 100, or leave progress unknown.";
        if (input.Conditions is null || input.Conditions.Length > 100 || input.Conditions.Any(x => x is null || string.IsNullOrWhiteSpace(x.Label) || x.Label.Length > 500)) return "Provide at most 100 nonempty checklist conditions of up to 500 characters.";
        if (input.TrackId is { } trackId && !await db.EconomyTracks.AnyAsync(x => x.Id == trackId, ct)) return "Choose an existing Track.";
        return null;
    }
    private static void Apply(Objective objective, ObjectiveInput input)
    {
        objective.Name = input.Name!.Trim(); objective.Description = input.Description ?? ""; objective.TrackId = input.TrackId;
        objective.Type = input.Type!; objective.Status = input.Status!; objective.TargetDate = input.TargetDate?.ToUniversalTime(); objective.ManualProgress = input.ManualProgress;
        objective.ConditionsJson = JsonSerializer.Serialize(input.Conditions); objective.Notes = input.Notes ?? ""; objective.UpdatedAt = DateTime.UtcNow;
        objective.CompletedAt = input.Status == "Completed" ? objective.CompletedAt ?? DateTime.UtcNow : null;
    }
}
