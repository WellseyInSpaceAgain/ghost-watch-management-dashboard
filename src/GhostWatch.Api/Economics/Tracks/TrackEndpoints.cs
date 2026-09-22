using GhostWatch.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GhostWatch.Api.Economics.Tracks;

public sealed record TrackInput(string? Name, string? Description, string? Status, string? Purpose, string? Notes, int? Revision);

public static class TrackEndpoints
{
    public static readonly string[] Statuses = ["Planning", "Active", "Paused", "Completed", "Archived"];
    public static readonly string[] Purposes = ["Cashflow", "R&D", "Strategic Supply", "Background Income", "Capital Growth", "Other"];

    public static void MapTracks(this WebApplication app)
    {
        var group = app.MapGroup("/api/economics/tracks");
        group.MapGet("/options", () => new { statuses = Statuses, purposes = Purposes });
        group.MapGet("/", async (bool? includeArchived, GhostWatchDbContext db, CancellationToken ct) =>
            await db.EconomyTracks.AsNoTracking()
                .Where(x => includeArchived == true || x.Status != "Archived")
                .OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct));
        group.MapGet("/{id:guid}", async (Guid id, GhostWatchDbContext db, CancellationToken ct) =>
            await db.EconomyTracks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) is { } track
                ? Results.Ok(track) : Results.NotFound());
        group.MapPost("/", async (TrackInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            var errors = Validate(input, false);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var track = new EconomyTrack();
            Apply(track, input);
            db.EconomyTracks.Add(track);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/economics/tracks/{track.Id}", track);
        });
        group.MapPut("/{id:guid}", async (Guid id, TrackInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            var errors = Validate(input, true);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var track = await db.EconomyTracks.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (track is null) return Results.NotFound();
            if (track.Revision != input.Revision) return Conflict();
            Apply(track, input);
            track.Revision++;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { return Conflict(); }
            return Results.Ok(track);
        });
        // No hard-delete endpoint: archived programmes retain their identity and history.
    }

    private static IResult Conflict() => Results.Problem(statusCode: 409,
        title: "This Track changed since you opened it.", detail: "Reload the latest version before saving your changes.");

    private static Dictionary<string, string[]> Validate(TrackInput input, bool editing)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 120)
            errors["name"] = ["Enter a Track name of 1–120 characters."];
        if (input.Description?.Length > 2000) errors["description"] = ["Description must be at most 2,000 characters."];
        if (input.Notes?.Length > 20000) errors["notes"] = ["Notes must be at most 20,000 characters."];
        if (input.Status is null || !Statuses.Contains(input.Status)) errors["status"] = ["Choose a supported Track status."];
        if (input.Purpose is null || !Purposes.Contains(input.Purpose)) errors["purpose"] = ["Choose a supported Track purpose."];
        if (editing && input.Revision is not > 0) errors["revision"] = ["The current revision is required when editing."];
        return errors;
    }

    private static void Apply(EconomyTrack track, TrackInput input)
    {
        track.Name = input.Name!.Trim();
        track.Description = input.Description?.Trim() ?? "";
        track.Purpose = input.Purpose!;
        track.Notes = input.Notes ?? "";
        track.UpdatedAt = DateTime.UtcNow;
        track.ArchivedAt = input.Status == "Archived" ? track.ArchivedAt ?? track.UpdatedAt : null;
        track.Status = input.Status!;
    }
}
