using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Eve.Inventory;
using Microsoft.EntityFrameworkCore;

namespace GhostWatch.Api.Eve.Esi;

public static class EveDataEndpoints
{
    public static void MapEveData(this WebApplication app)
    {
        app.MapPost("/api/eve/characters/{id:long}/refresh", async (long id, HttpContext context, GhostWatchDbContext db, RefreshQueue queue, CancellationToken ct) =>
        {
            if (context.Request.Headers["X-Ghost-Watch"] != "1" || context.Request.Headers["Sec-Fetch-Site"] == "cross-site") return Results.StatusCode(403);
            if (!await db.EveCharacters.AnyAsync(x => x.CharacterId == id, ct)) return Results.NotFound();
            return queue.Enqueue(id) ? Results.Accepted($"/api/eve/characters/{id}/data", queue.Status(id)) : Results.Conflict(queue.Status(id));
        });
        app.MapGet("/api/eve/characters/{id:long}/data", async (long id, GhostWatchDbContext db, RefreshQueue queue, CancellationToken ct) =>
        {
            var character = await db.EveCharacters.AsNoTracking().Where(x => x.CharacterId == id)
                .Select(x => new { x.CharacterId, x.CharacterName }).SingleOrDefaultAsync(ct);
            if (character is null) return Results.NotFound();
            var saved = await db.EveSections.AsNoTracking().Where(x => x.CharacterId == id).ToDictionaryAsync(x => x.Name, ct);
            var sections = CharacterRefresh.Sections.Select(name =>
            {
                saved.TryGetValue(name, out var row);
                return new { name, row?.AttemptedAt, row?.UpdatedAt, row?.Error, row?.Warning, data = row?.Json is { } json ? JsonNode.Parse(json) : null };
            }).ToArray();
            var skills = sections.Single(x => x.name == "skills").data;
            var jobs = await db.EveIndustryJobs.AsNoTracking().Where(x => x.CharacterId == id).OrderByDescending(x => x.StartDate).ToListAsync(ct);
            var assets = sections.Single(x => x.name == "assets").data;
            var blueprints = sections.Single(x => x.name == "blueprints").data;
            var typeKeys = new[] { assets, blueprints }.OfType<JsonArray>().SelectMany(x => x).Select(x => $"type:{x!["type_id"]}").Distinct().ToArray();
            var typeRows = await db.PublicEveLookups.AsNoTracking().Where(x => typeKeys.Contains(x.Key)).ToListAsync(ct);
            var metadata = typeRows.ToDictionary(x => x.Key, x => JsonNode.Parse(x.Json)!);
            var groupKeys = metadata.Values.Select(x => $"group:{x["group_id"]}").Distinct().ToArray();
            foreach (var row in await db.PublicEveLookups.AsNoTracking().Where(x => groupKeys.Contains(x.Key)).ToListAsync(ct)) metadata[row.Key] = JsonNode.Parse(row.Json)!;
            var inventory = InventoryProjection.Create(assets, blueprints, metadata);
            return Results.Ok(new { inventory, character, progress = queue.Status(id), sections, capacity = CapacityCalculator.Calculate(skills), jobs });
        });
    }
}
