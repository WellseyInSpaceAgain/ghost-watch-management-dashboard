using System.Text.Json;
using System.Text.Json.Nodes;

namespace GhostWatch.Api.Eve.Inventory;

public sealed record AssetRow(long ItemId, long TypeId, string Name, string Category, long Quantity,
    string Availability, long LocationId, string LocationFlag, string LocationName);
public sealed record StockRow(long TypeId, string Name, string Category, string Availability, long Quantity);
public sealed record BlueprintRow(long ItemId, long TypeId, string Name, string Kind, int Quantity,
    int MaterialEfficiency, int TimeEfficiency, int? RunsRemaining, long LocationId, string LocationFlag, string LocationName);
public sealed record InventoryView(List<AssetRow> Assets, List<StockRow> Stock, List<BlueprintRow> Blueprints);

public static class InventoryProjection
{
    public static void Validate(string section, JsonNode data)
    {
        if (data is not JsonArray rows) throw new JsonException();
        var ids = new HashSet<long>();
        foreach (var row in rows)
        {
            if (row is not JsonObject || row["item_id"]?.GetValue<long>() is not > 0 || row["type_id"]?.GetValue<long>() is not > 0 ||
                row["location_id"]?.GetValue<long>() is not > 0 || string.IsNullOrWhiteSpace(row["location_flag"]?.GetValue<string>())) throw new JsonException();
            if (!ids.Add(row["item_id"]!.GetValue<long>())) throw new JsonException("Duplicate inventory item across pages.");
            var quantity = row["quantity"]!.GetValue<int>();
            if (section == "assets")
            {
                if (quantity < 0 || string.IsNullOrWhiteSpace(row["location_type"]?.GetValue<string>())) throw new JsonException();
            }
            else
            {
                if (quantity is not (-1 or -2) && quantity <= 0) throw new JsonException();
                if (row["material_efficiency"]?.GetValue<int>() is not >= 0 || row["time_efficiency"]?.GetValue<int>() is not >= 0 ||
                    row["runs"]?.GetValue<int>() is not >= -1 || quantity == -2 && row["runs"]!.GetValue<int>() < 0) throw new JsonException();
            }
        }
    }

    public static InventoryView Create(JsonNode? assets, JsonNode? blueprints, IReadOnlyDictionary<string, JsonNode> metadata, IReadOnlyDictionary<long, string>? locations = null)
    {
        (string Name, string Category) Type(long id)
        {
            metadata.TryGetValue($"type:{id}", out var type);
            metadata.TryGetValue($"group:{type?["group_id"]}", out var group);
            var name = type?["name"]?.GetValue<string>() ?? $"Type {id}";
            return (name, Category(name, group?["name"]?.GetValue<string>(), group?["category_id"]?.GetValue<long>()));
        }
        var rows = assets?.AsArray().OfType<JsonObject>().ToArray() ?? [];
        var itemIds = rows.Select(x => x["item_id"]!.GetValue<long>()).ToHashSet();
        var parents = rows.ToDictionary(x => x["item_id"]!.GetValue<long>());
        string Location(long id, HashSet<long>? seen = null)
        {
            seen ??= [];
            if (seen.Count >= 32) return "Container location nesting limit reached";
            if (!seen.Add(id)) return $"Container {id} (location cycle)";
            if (parents.TryGetValue(id, out var parent))
                return $"Inside {Type(parent["type_id"]!.GetValue<long>()).Name} · {Location(parent["location_id"]!.GetValue<long>(), seen)}";
            if (locations?.TryGetValue(id, out var name) == true) return name;
            return id >= 1000000000000 ? $"Unresolved structure or container {id}"
                : id is >= 60000000 and < 64000000 ? $"Station {id} (name unavailable)"
                : id is >= 30000000 and < 33000000 ? $"System {id} (name unavailable)" : $"Location {id} (name unavailable)";
        }
        var assetRows = rows.Select(row =>
        {
            var id = row["type_id"]!.GetValue<long>(); var type = Type(id);
            return new AssetRow(row["item_id"]!.GetValue<long>(), id, type.Name, type.Category, row["quantity"]!.GetValue<long>(),
                Availability(row["location_type"]!.GetValue<string>(), row["location_flag"]!.GetValue<string>(), itemIds.Contains(row["location_id"]!.GetValue<long>())),
                row["location_id"]!.GetValue<long>(), row["location_flag"]!.GetValue<string>(), Location(row["location_id"]!.GetValue<long>()));
        }).OrderBy(x => x.Name).ThenBy(x => x.ItemId).ToList();
        var stock = assetRows.GroupBy(x => new { x.TypeId, x.Name, x.Category, x.Availability })
            .Select(g => new StockRow(g.Key.TypeId, g.Key.Name, g.Key.Category, g.Key.Availability, g.Sum(x => x.Quantity)))
            .OrderBy(x => x.Name).ThenBy(x => x.Availability).ToList();
        var blueprintRows = (blueprints?.AsArray().OfType<JsonObject>() ?? []).Select(row =>
        {
            var id = row["type_id"]!.GetValue<long>(); var quantity = row["quantity"]!.GetValue<int>();
            return new BlueprintRow(row["item_id"]!.GetValue<long>(), id, Type(id).Name, quantity == -2 ? "Copy" : "Original",
                quantity > 0 ? quantity : 1, row["material_efficiency"]!.GetValue<int>(), row["time_efficiency"]!.GetValue<int>(),
                quantity == -2 ? row["runs"]!.GetValue<int>() : null, row["location_id"]!.GetValue<long>(), row["location_flag"]!.GetValue<string>(), Location(row["location_id"]!.GetValue<long>()));
        }).OrderBy(x => x.Name).ThenBy(x => x.ItemId).ToList();
        return new(assetRows, stock, blueprintRows);
    }

    public static string Availability(string locationType, string flag, bool insideKnownItem)
    {
        if (insideKnownItem || locationType == "item" || flag.StartsWith("HiSlot", StringComparison.Ordinal) ||
            flag.StartsWith("MedSlot", StringComparison.Ordinal) || flag.StartsWith("LoSlot", StringComparison.Ordinal) ||
            flag.StartsWith("RigSlot", StringComparison.Ordinal) || flag.StartsWith("SubSystemSlot", StringComparison.Ordinal) ||
            flag is "Cargo" or "DroneBay" or "FighterBay" || flag.Contains("Hold", StringComparison.Ordinal)) return "Fitted / contained assets";
        return flag == "Hangar" && locationType is "station" or "other" ? "Available stock" : "Availability unknown";
    }

    public static string Category(string name, string? group, long? category)
    {
        if (category is null) return "Unknown category";
        if (category == 6) return "Ships";
        if (category is 7 or 32) return "Modules";
        if (category == 8) return "Ammunition";
        if (category == 9) return "Blueprints";
        if (category is 42 or 43) return "PI Commodities";
        if (category == 25) return "Ores and Ice";
        return group switch
        {
            "Mineral" => "Minerals", "Salvaged Materials" => "Salvage", "Ancient Salvage" => "Sleeper / Ancient Salvage",
            "Hybrid Polymers" => "Hybrid Polymers", "Hybrid Tech Components" => "T3 Components", "Datacores" => "Datacores",
            "Decryptors" or "Decryptors - Generic" or "Decryptors - Amarr" or "Decryptors - Caldari" or "Decryptors - Gallente" or "Decryptors - Minmatar" => "Decryptors",
            "Hybrid Charge" or "Advanced Blaster Charge" or "Advanced Railgun Charge" => "Ammunition",
            "Hybrid Weapon" => "Modules",
            "Gas Isotopes" when name.StartsWith("Fullerite-C", StringComparison.Ordinal) => "Fullerite Gas",
            "Ice Product" or "Moon Materials" or "Intermediate Materials" or "Composite" or "Construction Components" or "Advanced Capital Construction Components" => "Manufacturing Inputs",
            _ => category == 4 ? "Manufacturing Inputs" : "Other"
        };
    }
}
