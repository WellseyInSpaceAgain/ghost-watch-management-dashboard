using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GhostWatch.Api.Economics.Capital;

public sealed record PoolInput(string? Name, string? Description, string? Role, decimal? TargetCapital, bool Archived = false, int Revision = 0);
public sealed record AdjustmentInput(Guid? FromPoolId, Guid? ToPoolId, decimal Amount, string? Reason, string? Notes, DateTime? Date = null);
public static class CapitalEndpoints
{
    public static readonly string[] Roles = ["Other", "Core Capital", "Ghost Watch Treasury", "T3 R&D", "Expansion Capital"];
    private static IResult Invalid(string message) => Results.Problem(statusCode: 400, title: message);
    public static void MapCapital(this WebApplication app)
    {
        var group = app.MapGroup("/api/economics/capital");
        group.MapGet("/", Summary);
        group.MapPost("/pools", async (PoolInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (!Valid(input)) return Invalid("Provide a name, supported role and nonnegative target. Names are limited to 120 characters and descriptions to 2,000.");
            if (!input.Archived && input.Role != "Other" && await db.CapitalPools.AnyAsync(x => x.Role == input.Role && x.ArchivedAt == null, ct)) return Invalid("An active pool already has this programme role.");
            var pool = new CapitalPool(); Apply(pool, input); db.CapitalPools.Add(pool); await db.SaveChangesAsync(ct);
            return Results.Created($"/api/economics/capital/pools/{pool.Id}", pool);
        });
        group.MapPut("/pools/{id:guid}", async (Guid id, PoolInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (!Valid(input)) return Invalid("Provide a valid pool name, role and nonnegative target.");
            var pool = await db.CapitalPools.FindAsync([id], ct);
            if (pool is null) return Results.NotFound();
            if (pool.Revision != input.Revision) return Conflict();
            if (!input.Archived && input.Role != "Other" && await db.CapitalPools.AnyAsync(x => x.Id != id && x.Role == input.Role && x.ArchivedAt == null, ct)) return Invalid("An active pool already has this programme role.");
            Apply(pool, input); pool.Revision++;
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return Conflict(); }
            return Results.Ok(pool);
        });
        group.MapPost("/adjustments", async (AdjustmentInput input, GhostWatchDbContext db, CancellationToken ct) =>
        {
            if (input.Amount <= 0 || input.Amount > 1000000000000000m || input.FromPoolId == input.ToPoolId || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 500 || input.Notes?.Length > 2000)
                return Invalid("Choose different source/destination pools (one can be unallocated), a positive amount, and a reason up to 500 characters.");
            var from = input.FromPoolId is { } fromId ? await db.CapitalPools.FindAsync([fromId], ct) : null;
            var to = input.ToPoolId is { } toId ? await db.CapitalPools.FindAsync([toId], ct) : null;
            if (input.FromPoolId is not null && from is null || input.ToPoolId is not null && to is null) return Invalid("A selected pool was not found.");
            if (from?.ArchivedAt is not null || to?.ArchivedAt is not null) return Invalid("Restore archived pools before adjusting them.");
            if (from is not null && from.AllocatedCapital < input.Amount) return Invalid("The source pool has insufficient conceptual allocation. Adjust the allocation first.");
            if (from is not null) { from.AllocatedCapital -= input.Amount; from.Revision++; from.UpdatedAt = DateTime.UtcNow; }
            if (to is not null) { to.AllocatedCapital += input.Amount; to.Revision++; to.UpdatedAt = DateTime.UtcNow; }
            var adjustment = new CapitalAdjustment { FromPoolId = input.FromPoolId, ToPoolId = input.ToPoolId, Amount = input.Amount,
                Reason = input.Reason.Trim(), Notes = input.Notes ?? "", Date = (input.Date ?? DateTime.UtcNow).ToUniversalTime() };
            db.CapitalAdjustments.Add(adjustment);
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return Conflict(); }
            return Results.Created($"/api/economics/capital/adjustments/{adjustment.Id}", adjustment);
        });
    }
    public static async Task<IResult> Summary(GhostWatchDbContext db, CancellationToken ct)
    {
        var pools = await db.CapitalPools.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        var characters = await db.EveCharacters.CountAsync(ct);
        var wallets = await db.EveSections.AsNoTracking().Where(x => x.Name == "wallet").ToListAsync(ct);
        var known = wallets.Where(x => x.Json is not null).ToArray();
        var knownLiquid = known.Sum(x => JsonNode.Parse(x.Json!)!.GetValue<decimal>());
        decimal? liquid = characters > 0 && known.Length == characters ? knownLiquid : null;
        var total = pools.Sum(x => x.AllocatedCapital); // Archival never silently deallocates capital.
        var adjustments = (await db.CapitalAdjustments.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync(ct))
            .Select(x => new { x.Id, x.Date, x.FromPoolId, x.ToPoolId, fromName = pools.Find(p => p.Id == x.FromPoolId)?.Name ?? "Unallocated", toName = pools.Find(p => p.Id == x.ToPoolId)?.Name ?? "Unallocated", x.Amount, x.Reason, x.Notes });
        return Results.Ok(new { pools, adjustments, roles = Roles, allocated = total, liquid, knownLiquid,
            walletCount = known.Length, characterCount = characters, walletsStale = known.Any(x => x.Error is not null || x.UpdatedAt == null || x.UpdatedAt < DateTime.UtcNow.AddDays(-1)),
            overAllocated = liquid is { } value ? Math.Max(0, total - value) : (decimal?)null });
    }
    private static IResult Conflict() => Results.Problem(statusCode: 409, title: "Capital changed in another operation. Reload before retrying.");
    private static bool Valid(PoolInput input) => !string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 120 && input.Description?.Length is not > 2000 && input.Role is not null && Roles.Contains(input.Role) && input.TargetCapital is not < 0;
    private static void Apply(CapitalPool pool, PoolInput input)
    { pool.Name = input.Name!.Trim(); pool.Description = input.Description ?? ""; pool.Role = input.Role!; pool.TargetCapital = input.TargetCapital; pool.UpdatedAt = DateTime.UtcNow; pool.ArchivedAt = input.Archived ? pool.ArchivedAt ?? DateTime.UtcNow : null; }
}
