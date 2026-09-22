using System.Text.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Eve.Inventory;
using GhostWatch.Api.Eve.Auth;
using Microsoft.EntityFrameworkCore;

namespace GhostWatch.Api.Eve.Esi;

public sealed class CharacterRefresh(GhostWatchDbContext db, EsiClient esi, ICharacterAccessTokens tokens, InventoryMetadata metadata, LocationNames locations)
{
    public static readonly string[] Sections = ["wallet", "skills", "skillQueue", "industryJobs", "marketOrders", "assets", "blueprints"];
    public static string SafeError(Exception error) => error switch
    {
        EsiException or SsoException => error.Message,
        OperationCanceledException => "The EVE request timed out. Try again.",
        _ => "EVE data could not be collected. Previous data has been retained."
    };

    public async Task<bool> Refresh(long id, Action<string> progress, CancellationToken ct)
    {
        if (!await db.EveCharacters.AnyAsync(x => x.CharacterId == id, ct)) return false;
        var sections = await db.EveSections.Where(x => x.CharacterId == id).ToDictionaryAsync(x => x.Name, ct);
        var complete = true;
        SsoException? authenticationError = null;
        foreach (var name in Sections)
        {
            ct.ThrowIfCancellationRequested();
            progress(name);
            if (!sections.TryGetValue(name, out var section))
            {
                section = new() { CharacterId = id, Name = name };
                db.EveSections.Add(section);
            }
            section.AttemptedAt = DateTime.UtcNow;
            var succeeded = false;
            JsonNode? data = null;
            string? token = null;
            List<EveIndustryJob>? jobs = null;
            try
            {
                if (authenticationError is not null) throw authenticationError;
                try { token = await tokens.Get(id, ct); }
                catch (SsoException error) { authenticationError = error; throw; }
                var endpoint = name switch
                {
                    "wallet" => "wallet", "skills" => "skills", "skillQueue" => "skillqueue",
                    "industryJobs" => "industry/jobs?include_completed=true", "marketOrders" => "orders", "assets" => "assets", "blueprints" => "blueprints",
                    _ => throw new InvalidOperationException()
                };
                data = name is "assets" or "blueprints"
                    ? await esi.Pages($"characters/{id}/{endpoint}", token, id, ct)
                    : await esi.Get($"characters/{id}/{endpoint}", token, ct, id);
                Validate(name, data);
                // Parse the complete response before touching any persisted industry rows.
                if (name == "industryJobs") jobs = data.AsArray().Select(row => ParseJob(id, row!)).ToList();
                if (jobs is not null && jobs.Select(x => x.JobId).Distinct().Count() != jobs.Count) throw new JsonException();
                succeeded = true;
            }
            catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                section.Error = SafeError(error);
                complete = false;
            }
            if (succeeded)
            {
                // Validation errors must not replace previously valid facts.
                if (data is not null)
                {
                    if (jobs is not null)
                    {
                        var existing = await db.EveIndustryJobs.Where(x => x.CharacterId == id).ToDictionaryAsync(x => x.JobId, ct);
                        foreach (var job in jobs)
                        {
                            if (existing.TryGetValue(job.JobId, out var old)) db.Entry(old).CurrentValues.SetValues(job);
                            else db.EveIndustryJobs.Add(job);
                        }
                    }
                    var jobTypes = jobs is null ? null : new JsonArray(db.EveIndustryJobs.Local.Where(job => job.CharacterId == id).SelectMany(job => new long?[] { job.BlueprintTypeId, job.ProductTypeId }).OfType<long>().Distinct()
                        .Select(type => (JsonNode)new JsonObject { ["type_id"] = type }).ToArray());
                    section.Warning = jobTypes is not null ? await metadata.Collect(jobTypes, ct) : name is "assets" or "blueprints" ? await metadata.Collect(data.AsArray(), ct) : null;
                    if (name is "assets" or "blueprints")
                    {
                        var assets = name == "assets" ? data.AsArray() : await db.EveSections.Where(x => x.CharacterId == id && x.Name == "assets")
                            .Select(x => x.Json).SingleOrDefaultAsync(ct) is { } json ? JsonNode.Parse(json)!.AsArray() : null;
                        var warning = await locations.Collect(id, token!, data.AsArray(), assets, ct);
                        section.Warning = string.Join(" ", new[] { section.Warning, warning }.Where(x => x is not null));
                        if (section.Warning.Length == 0) section.Warning = null;
                    }
                    if (section.Warning is not null) complete = false;
                    section.Json = data.ToJsonString();
                    section.UpdatedAt = DateTime.UtcNow;
                    section.Error = null;
                }
            }
            // Each section and its factual rows commit together. Never modify Economics tables.
            await db.SaveChangesAsync(ct);
        }
        return complete;
    }

    public static void Validate(string name, JsonNode data)
    {
        if (name is "assets" or "blueprints") { InventoryProjection.Validate(name, data); return; }
        if (name == "wallet") { _ = data.GetValue<decimal>(); return; }
        if (name == "skills")
        {
            if (data["skills"] is not JsonArray skills) throw new JsonException();
            foreach (var row in skills)
            {
                if (row?["skill_id"]?.GetValue<long>() is not > 0 || row["trained_skill_level"]?.GetValue<int>() is not (>= 0 and <= 5)) throw new JsonException();
                if (row["active_skill_level"] is { } active && active.GetValue<int>() is not (>= 0 and <= 5)) throw new JsonException();
            }
            return;
        }
        if (data is not JsonArray array || array.Any(row => row is not JsonObject)) throw new JsonException();
    }

    private static EveIndustryJob ParseJob(long id, JsonNode row)
    {
        var jobId = row["job_id"]!.GetValue<long>();
        if (jobId <= 0) throw new JsonException();
        return new() { CharacterId = id, JobId = jobId, ActivityId = row["activity_id"]!.GetValue<int>(),
            BlueprintTypeId = row["blueprint_type_id"]!.GetValue<long>(), ProductTypeId = row["product_type_id"]?.GetValue<long>(),
            Runs = row["runs"]!.GetValue<int>(), Status = row["status"]!.GetValue<string>(),
            StartDate = row["start_date"]!.GetValue<DateTime>().ToUniversalTime(), EndDate = row["end_date"]!.GetValue<DateTime>().ToUniversalTime(),
            LastSeenAt = DateTime.UtcNow, RawJson = row.ToJsonString() };
    }
}
