using GhostWatch.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GhostWatch.Api.Management;

public sealed record AccountInput(string? Name, string? Subscription, string? Notes, int Revision = 0);
public sealed record CharacterPlanInput(Guid? AccountId, string? Assignment, string? Notes, Guid[]? TrackIds, int Revision = 0);

public static class ManagementEndpoints
{
    private static IResult Invalid(string message) => Results.Problem(statusCode: 400, title: message);
    private static IResult Conflict() => Results.Problem(statusCode: 409, title: "This record changed. Reload before saving.");
    public static void MapCharacterManagement(this WebApplication app)
    {
        var group = app.MapGroup("/api/management");
        group.MapGet("/accounts", async (GhostWatchDbContext db, CancellationToken ct) =>
            await db.ManagedAccounts.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));
        group.MapPost("/accounts", async (AccountInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (!Valid(input)) return Invalid("Enter an account name (up to 120 characters), subscription Unknown/Alpha/Omega and notes up to 2,000 characters.");
            var account = new ManagedAccount { Name = input.Name!.Trim(), Subscription = input.Subscription!, Notes = input.Notes ?? "" };
            db.ManagedAccounts.Add(account); await db.SaveChangesAsync(ct);
            return Results.Created($"/api/management/accounts/{account.Id}", account);
        });
        group.MapPut("/accounts/{id:guid}", async (Guid id, AccountInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (!Valid(input)) return Invalid("Enter a valid name, subscription and notes.");
            var account = await db.ManagedAccounts.FindAsync([id], ct);
            if (account is null) return Results.NotFound();
            if (account.Revision != input.Revision) return Conflict();
            account.Name = input.Name!.Trim(); account.Subscription = input.Subscription!; account.Notes = input.Notes ?? ""; account.Revision++;
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return Conflict(); }
            return Results.Ok(account);
        });
        group.MapGet("/characters/{id:long}", async (long id, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (!await db.EveCharacters.AnyAsync(x => x.CharacterId == id, ct)) return Results.NotFound();
            var plan = await db.CharacterPlans.AsNoTracking().Include(x => x.Account).SingleOrDefaultAsync(x => x.CharacterId == id, ct);
            var tracks = await db.CharacterTracks.AsNoTracking().Where(x => x.CharacterId == id).Select(x => new { x.TrackId, x.Track.Name }).ToListAsync(ct);
            return Results.Ok(new { characterId = id, plan?.AccountId, accountName = plan?.Account?.Name, subscription = plan?.Account?.Subscription ?? "Unknown",
                assignment = plan?.Assignment ?? "", notes = plan?.Notes ?? "", revision = plan?.Revision ?? 0, tracks });
        });
        group.MapPut("/characters/{id:long}", async (long id, CharacterPlanInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (!await db.EveCharacters.AnyAsync(x => x.CharacterId == id, ct)) return Results.NotFound();
            if (input.Assignment?.Length > 120 || input.Notes?.Length > 2000 || input.TrackIds is null || input.TrackIds.Length > 100)
                return Invalid("Assignment must be at most 120 characters, notes 2,000 characters and links at most 100 Tracks.");
            if (input.AccountId is { } accountId && !await db.ManagedAccounts.AnyAsync(x => x.Id == accountId, ct)) return Invalid("Account was not found.");
            var ids = input.TrackIds.Distinct().ToArray();
            if (await db.EconomyTracks.CountAsync(x => ids.Contains(x.Id), ct) != ids.Length) return Invalid("One or more Tracks were not found.");
            var plan = await db.CharacterPlans.FindAsync([id], ct);
            if ((plan?.Revision ?? 0) != input.Revision) return Conflict();
            if (plan is null) { plan = new() { CharacterId = id, Revision = 0 }; db.CharacterPlans.Add(plan); }
            plan.AccountId = input.AccountId; plan.Assignment = input.Assignment?.Trim() ?? ""; plan.Notes = input.Notes ?? ""; plan.Revision++;
            var existing = await db.CharacterTracks.Where(x => x.CharacterId == id).ToListAsync(ct);
            db.CharacterTracks.RemoveRange(existing.Where(x => !ids.Contains(x.TrackId)));
            foreach (var trackId in ids.Except(existing.Select(x => x.TrackId))) db.CharacterTracks.Add(new() { CharacterId = id, TrackId = trackId });
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return Conflict(); }
            return Results.Ok(new { plan.Revision });
        });
        group.MapGet("/tracks/{id:guid}/characters", async (Guid id, GhostWatchDbContext db, CancellationToken ct) =>
            await db.CharacterTracks.AsNoTracking().Where(x => x.TrackId == id)
                .Select(x => new { x.CharacterId, x.Character.CharacterName }).ToListAsync(ct));
    }
    private static bool Valid(AccountInput input) => !string.IsNullOrWhiteSpace(input.Name) && input.Name.Trim().Length <= 120
        && input.Subscription is "Unknown" or "Alpha" or "Omega" && input.Notes?.Length is not > 2000;
}
