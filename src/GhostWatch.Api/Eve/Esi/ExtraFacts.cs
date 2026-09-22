using System.Text.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Eve.Inventory;

namespace GhostWatch.Api.Eve.Esi;

// Enrichment uses public caches. Raw ESI section payloads remain unchanged.
public sealed class ExtraFacts(GhostWatchDbContext db, EsiClient esi, InventoryMetadata metadata, LocationNames locations)
{
    public static readonly string[] Names = ["skills", "skillQueue", "marketOrders", "standings", "loyalty", "planets"];
    private static IEnumerable<JsonNode> Rows(string name, JsonNode data) => (name == "skills" ? data["skills"] : data).Rows();
    public async Task<string?> Collect(string section, JsonNode data, long characterId, string token, CancellationToken ct)
    {
        var rows = Rows(section, data).ToArray();
        var warnings = new List<string>();
        if (section is "skills" or "skillQueue" or "marketOrders")
        {
            var ids = rows.Select(x => x[section == "marketOrders" ? "type_id" : "skill_id"]!.GetValue<long>()).Distinct();
            var types = new JsonArray(ids.Select(id => (JsonNode)new JsonObject { ["type_id"] = id }).ToArray());
            if (await metadata.Collect(types, ct) is { } warning) warnings.Add(warning);
        }
        if (section == "marketOrders" && await locations.Collect(characterId, token, data.AsArray(), null, ct) is { } locationWarning) warnings.Add(locationWarning);
        try
        {
            foreach (var id in rows.SelectMany(x => new[] { "from_id", "corporation_id", "solar_system_id" }.Select(field => x[field]?.GetValue<long>() ?? 0)).Where(x => x > 0).Distinct())
                await PublicName(id, false, ct);
            foreach (var id in rows.Select(x => x["planet_id"]?.GetValue<long>() ?? 0).Where(x => x > 0).Distinct())
                await PublicName(id, true, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
        { warnings.Add("Records collected, but some public names are unavailable. Try refreshing later."); }
        return warnings.Count == 0 ? null : string.Join(" ", warnings.Distinct());
    }
    private async Task PublicName(long id, bool planet, CancellationToken ct)
    {
        var key = $"{(planet ? "planet" : "name")}:{id}";
        var row = await db.PublicEveLookups.FindAsync([key], ct);
        if (row is not null && row.ExpiresAt > DateTime.UtcNow) return;
        JsonNode response;
        if (planet) response = await esi.Get($"universe/planets/{id}", null, ct);
        else
        {
            var (data, _) = await esi.Request("universe/names", null, ct, new JsonArray(JsonValue.Create(id)));
            response = data.AsArray().Single(x => x?["id"]?.GetValue<long>() == id)!;
        }
        if (string.IsNullOrWhiteSpace(response["name"]?.GetValue<string>())) throw new JsonException();
        if (row is null) { row = new() { Key = key }; db.PublicEveLookups.Add(row); }
        row.Json = response.ToJsonString(); row.ExpiresAt = DateTime.UtcNow.AddDays(30);
    }
    public static JsonNode Project(string section, JsonNode data, IReadOnlyDictionary<string, JsonNode> lookups, IReadOnlyDictionary<long, string> locations)
    {
        var clone = data.DeepClone();
        foreach (var row in Rows(section, clone))
        {
            foreach (var field in new[] { "skill_id", "type_id", "from_id", "corporation_id", "solar_system_id", "planet_id" })
            {
                if (row[field] is not { } value) continue;
                var id = value.GetValue<long>();
                var prefix = field is "skill_id" or "type_id" ? "type" : field == "planet_id" ? "planet" : "name";
                var info = lookups.GetValueOrDefault($"{prefix}:{id}");
                row[field[..^3] + "_name"] = info?["name"]?.GetValue<string>() ?? $"Name unavailable ({id})";
                if (field == "skill_id") row["group_name"] = lookups.GetValueOrDefault($"group:{info?["group_id"]}")?["name"]?.GetValue<string>();
            }
            if (row["location_id"] is { } location) row["location_name"] = locations.GetValueOrDefault(location.GetValue<long>(), $"Location name unavailable ({location})");
            if (section == "marketOrders")
            {
                row["side"] = row["is_buy_order"]?.GetValue<bool>() == true ? "Buy" : "Sell";
                row["remaining_value"] = row["price"]!.GetValue<decimal>() * row["volume_remain"]!.GetValue<long>();
            }
        }
        return clone;
    }
}
