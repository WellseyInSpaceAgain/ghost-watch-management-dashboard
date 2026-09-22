using System.Text.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Eve.Esi;
using GhostWatch.Api.Eve.Auth;

namespace GhostWatch.Api.Eve.Inventory;

public sealed class LocationNames(GhostWatchDbContext db, EsiClient esi)
{
    public static bool IsPublic(long id) => id is >= 60000000 and < 64000000 or >= 30000000 and < 33000000;

    public async Task<string?> Collect(long characterId, string token, JsonArray inventory, JsonArray? assets, CancellationToken ct)
    {
        var itemIds = (assets ?? []).Select(row => row!["item_id"]!.GetValue<long>()).ToHashSet();
        var incomplete = false;
        var missingPermission = false;
        foreach (var row in inventory.DistinctBy(row => row!["location_id"]!.GetValue<long>()))
        {
            var id = row!["location_id"]!.GetValue<long>();
            if (itemIds.Contains(id) || row["location_type"]?.GetValue<string>() == "item") continue;
            try
            {
                if (IsPublic(id)) await PublicName(id, ct);
                else if (id >= 1000000000000)
                {
                    var character = await db.EveCharacters.FindAsync([characterId], ct);
                    if (!EveScopes.Allows(character?.GrantedScopesJson, "structures"))
                    { missingPermission = true; continue; }
                    var saved = await db.EveLocationNames.FindAsync([characterId, id], ct);
                    if (saved is not null && saved.ExpiresAt > DateTime.UtcNow)
                    { incomplete |= saved.Name is null; continue; }
                    string? name;
                    try
                    {
                        var response = await esi.Get($"universe/structures/{id}", token, ct, characterId);
                        name = response["name"]?.GetValue<string>();
                        if (string.IsNullOrWhiteSpace(name)) throw new JsonException();
                    }
                    catch (EsiException error) when (error.Status is 403 or 404)
                    {
                        // Access can be revoked. Clear previously cached names on explicit denial.
                        name = null;
                    }
                    if (saved is null) { saved = new() { CharacterId = characterId, LocationId = id }; db.EveLocationNames.Add(saved); }
                    saved.Name = name;
                    saved.ExpiresAt = DateTime.UtcNow.Add(name is null ? TimeSpan.FromMinutes(5) : TimeSpan.FromHours(1));
                    incomplete |= name is null;
                }
                else incomplete = true;
            }
            catch (EsiException error) when (error.Status is 400 or 404) { incomplete = true; }
            catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
            { incomplete = true; break; }
        }
        if (missingPermission) return EveScopes.MissingMessage("structures");
        return incomplete ? "Some location names are unavailable. EVE may deny structure access even with permission. Check ESI Permissions on this character and retry later." : null;
    }

    private async Task PublicName(long id, CancellationToken ct)
    {
        var key = $"location:{id}";
        var saved = await db.PublicEveLookups.FindAsync([key], ct);
        if (saved is not null && saved.ExpiresAt > DateTime.UtcNow) return;
        var (response, _) = await esi.Request("universe/names", null, ct, new JsonArray(JsonValue.Create(id)));
        var name = response.AsArray().SingleOrDefault(row => row?["id"]?.GetValue<long>() == id)?["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name)) throw new JsonException();
        if (saved is null) { saved = new() { Key = key }; db.PublicEveLookups.Add(saved); }
        saved.Json = new JsonObject { ["name"] = name }.ToJsonString();
        saved.ExpiresAt = DateTime.UtcNow.AddDays(30);
    }
}
