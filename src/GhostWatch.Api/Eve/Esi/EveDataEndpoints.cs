using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Eve.Inventory;
using GhostWatch.Api.Eve.Auth;
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
            // Capture progress before reading facts: completion must not accompany a pre-refresh snapshot.
            var progress = queue.Status(id);
            var character = await db.EveCharacters.AsNoTracking().Where(x => x.CharacterId == id)
                .Select(x => new { x.CharacterId, x.CharacterName, x.GrantedScopesJson }).SingleOrDefaultAsync(ct);
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
            var typeKeys = new[] { assets, blueprints }.OfType<JsonArray>().SelectMany(x => x).Select(x => $"type:{x!["type_id"]}").Concat(jobs.SelectMany(job => new long?[] { job.BlueprintTypeId, job.ProductTypeId }).OfType<long>().Select(type => $"type:{type}")).Distinct().ToArray();
            var typeRows = await db.PublicEveLookups.AsNoTracking().Where(x => typeKeys.Contains(x.Key)).ToListAsync(ct);
            var metadata = typeRows.ToDictionary(x => x.Key, x => JsonNode.Parse(x.Json)!);
            var groupKeys = metadata.Values.Select(x => $"group:{x["group_id"]}").Distinct().ToArray();
            foreach (var row in await db.PublicEveLookups.AsNoTracking().Where(x => groupKeys.Contains(x.Key)).ToListAsync(ct)) metadata[row.Key] = JsonNode.Parse(row.Json)!;
            var locationIds = new[] { assets, blueprints }.OfType<JsonArray>().SelectMany(x => x).Select(x => x!["location_id"]!.GetValue<long>()).Distinct().ToArray();
            var publicKeys = locationIds.Where(LocationNames.IsPublic).Select(x => $"location:{x}").ToArray();
            var locationNames = (await db.PublicEveLookups.AsNoTracking().Where(x => publicKeys.Contains(x.Key)).ToListAsync(ct))
                .ToDictionary(x => long.Parse(x.Key[9..]), x => JsonNode.Parse(x.Json)!["name"]!.GetValue<string>());
            var now = DateTime.UtcNow;
            foreach (var location in await db.EveLocationNames.AsNoTracking().Where(x => x.CharacterId == id && locationIds.Contains(x.LocationId)).ToListAsync(ct))
                if (EveScopes.Allows(character.GrantedScopesJson, "structures") && location.Name is not null && location.ExpiresAt > now) locationNames[location.LocationId] = location.Name;
            var inventory = InventoryProjection.Create(assets, blueprints, metadata, locationNames);
            string TypeName(long type) => metadata.GetValueOrDefault($"type:{type}")?["name"]?.GetValue<string>() ?? $"Type {type} (name unavailable)";
            var namedJobs = jobs.Select(job => new { job.JobId, job.ActivityId, job.ProductTypeId, job.BlueprintTypeId,
                productName = TypeName(job.ProductTypeId ?? job.BlueprintTypeId), blueprintName = TypeName(job.BlueprintTypeId),
                job.Runs, job.Status, job.StartDate, job.EndDate, job.LastSeenAt });
            return Results.Ok(new { inventory, character = new { character.CharacterId, character.CharacterName }, permissions = EveScopes.Permissions(character.GrantedScopesJson), progress, sections, capacity = CapacityCalculator.Calculate(skills), jobs = namedJobs });
        });
    }
}
