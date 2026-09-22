using System.Text.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Eve.Esi;

namespace GhostWatch.Api.Eve.Inventory;

public sealed class InventoryMetadata(GhostWatchDbContext db, EsiClient esi)
{
    public async Task<string?> Collect(JsonArray inventory, CancellationToken ct)
    {
        var incomplete = false;
        foreach (var id in inventory.Select(row => row!["type_id"]!.GetValue<long>()).Distinct())
        {
            try
            {
                var type = await Cached($"type:{id}", $"universe/types/{id}", false, ct);
                var groupId = type["group_id"]!.GetValue<long>();
                await Cached($"group:{groupId}", $"universe/groups/{groupId}", true, ct);
            }
            catch (EsiException error) when (error.Status == 404) { incomplete = true; }
            catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // A service/rate-limit outage should not trigger another lookup for every item type.
                incomplete = true;
                break;
            }
        }
        return incomplete ? "Inventory collected, but some names or categories could not be refreshed. Known names and type IDs are shown. Try refreshing again later." : null;
    }

    private async Task<JsonNode> Cached(string key, string path, bool group, CancellationToken ct)
    {
        var row = await db.PublicEveLookups.FindAsync([key], ct);
        if (row is not null && row.ExpiresAt > DateTime.UtcNow) return JsonNode.Parse(row.Json)!;
        var data = await esi.Get(path, null, ct);
        if (string.IsNullOrWhiteSpace(data["name"]?.GetValue<string>()) || data[group ? "category_id" : "group_id"]?.GetValue<long>() is not > 0)
            throw new JsonException("Incomplete public metadata.");
        if (row is null) { row = new() { Key = key }; db.PublicEveLookups.Add(row); }
        row.Json = data.ToJsonString();
        row.ExpiresAt = DateTime.UtcNow.AddDays(30);
        // The caller commits metadata alongside the successfully fetched section.
        return data;
    }
}
