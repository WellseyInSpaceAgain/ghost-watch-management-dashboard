using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Economics.Tracks;
using GhostWatch.Api.Eve.Auth;
using GhostWatch.Api.Eve.Esi;
using GhostWatch.Api.Eve.Inventory;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace GhostWatch.Tests;

public class InventoryTests
{
    [Theory]
    [InlineData("station", "Hangar", false, "Available stock")]
    [InlineData("other", "Hangar", false, "Available stock")]
    [InlineData("station", "Hangar", true, "Fitted / contained assets")]
    [InlineData("item", "Hangar", false, "Fitted / contained assets")]
    [InlineData("other", "HiSlot0", false, "Fitted / contained assets")]
    [InlineData("other", "Cargo", false, "Fitted / contained assets")]
    [InlineData("other", "SpecializedOreHold", false, "Fitted / contained assets")]
    [InlineData("solar_system", "Hangar", false, "Availability unknown")]
    public void Availability_does_not_treat_fitted_or_contained_items_as_stock(string location, string flag, bool contained, string expected)
        => Assert.Equal(expected, InventoryProjection.Availability(location, flag, contained));

    [Theory]
    [InlineData("Hybrid weapon", "Hybrid Weapon", 7, "Modules")]
    [InlineData("Ancient anything", "Unknown group", 1, "Other")]
    [InlineData("Polymer", "Hybrid Polymers", 4, "Hybrid Polymers")]
    [InlineData("Fullerite-C28", "Gas Isotopes", 4, "Fullerite Gas")]
    [InlineData("Other gas", "Gas Isotopes", 4, "Manufacturing Inputs")]
    public void Categories_use_exact_metadata_not_name_guesses(string name, string group, long category, string expected)
        => Assert.Equal(expected, InventoryProjection.Category(name, group, category));

    [Fact]
    public void Blueprint_copy_runs_originals_and_stacked_originals_remain_distinct()
    {
        var rows = JsonNode.Parse($"[{Blueprint(1, -2, 0)},{Blueprint(2, -1, -1)},{Blueprint(3, 5, -1)}]")!;
        InventoryProjection.Validate("blueprints", rows);
        var view = InventoryProjection.Create(null, rows, new Dictionary<string, JsonNode>());
        Assert.Equal("Copy", view.Blueprints[0].Kind);
        Assert.Equal(0, view.Blueprints[0].RunsRemaining);
        Assert.Null(view.Blueprints[1].RunsRemaining);
        Assert.Equal(5, view.Blueprints[2].Quantity);
        Assert.Equal("Type 100", view.Blueprints[0].Name);
        Assert.Equal(10, view.Blueprints[0].MaterialEfficiency);
        Assert.Equal(20, view.Blueprints[0].TimeEfficiency);
    }

    [Fact]
    public void Stock_aggregation_keeps_availability_separate_and_unknown_metadata_explicit()
    {
        var rows = JsonNode.Parse($"[{Asset(1, 10)},{Asset(2, 20)},{Asset(3, 5, 1)}]")!;
        var view = InventoryProjection.Create(rows, null, new Dictionary<string, JsonNode>());
        Assert.Equal(2, view.Stock.Count);
        Assert.Equal(30, view.Stock.Single(x => x.Availability == "Available stock").Quantity);
        Assert.Equal(5, view.Stock.Single(x => x.Availability == "Fitted / contained assets").Quantity);
        Assert.All(view.Stock, row => Assert.Equal("Unknown category", row.Category));
        Assert.ThrowsAny<Exception>(() => InventoryProjection.Validate("assets", JsonNode.Parse($"[{Asset(1, 10)},{Asset(1, 20)}]")!));
    }

    [Fact]
    public async Task Full_pagination_raw_data_and_local_notes_survive_failed_second_pages()
    {
        var failSecondPage = false;
        var failMetadata = false;
        var metadataCalls = 0;
        using var upstream = new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.StartsWith("/universe/"))
            {
                Assert.Null(request.Headers.Authorization);
                metadataCalls++;
                if (failMetadata) return new(HttpStatusCode.NotFound);
                return Json(path.Contains("types") ? "{\"name\":\"Tritanium\",\"group_id\":18}" : "{\"name\":\"Mineral\",\"category_id\":4}");
            }
            Assert.Equal("Bearer test-token", request.Headers.Authorization!.ToString());
            if (path.EndsWith("assets") || path.EndsWith("blueprints"))
            {
                var second = request.RequestUri.Query.Contains("page=2");
                if (second && failSecondPage) return new(HttpStatusCode.Forbidden);
                var result = Json("[" + (path.EndsWith("assets") ? Asset(second ? 2 : 1, second ? 20 : 10) : Blueprint(second ? 12 : 11, second ? -2 : -1, second ? 7 : -1)) + "]");
                result.Headers.Add("X-Pages", "2"); return result;
            }
            return Json(path.EndsWith("wallet") ? "10" : path.EndsWith("skills") ? "{\"skills\":[]}" : "[]");
        });
        await using var app = new TestApplication(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<ICharacterAccessTokens, Tokens>();
            services.AddHttpClient<EsiClient>().ConfigurePrimaryHttpMessageHandler(() => upstream);
        }));
        using var browser = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
        db.EveCharacters.Add(new() { CharacterId = 7, CharacterName = "Inventory pilot" });
        db.EconomyTracks.Add(new EconomyTrack { Name = "Preserve", Notes = "Local planning" });
        await db.SaveChangesAsync();
        var refresh = scope.ServiceProvider.GetRequiredService<CharacterRefresh>();
        Assert.True(await refresh.Refresh(7, _ => { }, default));
        var assets = await db.EveSections.SingleAsync(x => x.Name == "assets");
        var blueprints = await db.EveSections.SingleAsync(x => x.Name == "blueprints");
        var oldAssets = assets.Json; var oldBlueprints = blueprints.Json; var oldTime = assets.UpdatedAt;
        Assert.Equal(2, JsonNode.Parse(oldAssets!)!.AsArray().Count);
        Assert.DoesNotContain("Tritanium", oldAssets); // Raw payload is preserved without enrichment edits.
        Assert.Equal(2, metadataCalls); // Type/group cache reused across items and sections.
        Assert.True(await refresh.Refresh(7, _ => { }, default));
        Assert.Equal(2, metadataCalls);
        oldTime = assets.UpdatedAt;
        failSecondPage = true;
        Assert.False(await refresh.Refresh(7, _ => { }, default));
        Assert.Equal(oldAssets, assets.Json); Assert.Equal(oldBlueprints, blueprints.Json);
        Assert.Equal(oldTime, assets.UpdatedAt); Assert.NotNull(assets.Error); Assert.NotNull(blueprints.Error);
        Assert.Equal("Local planning", (await db.EconomyTracks.SingleAsync()).Notes);
        failSecondPage = false; failMetadata = true;
        foreach (var metadata in await db.PublicEveLookups.ToListAsync()) metadata.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();
        Assert.False(await refresh.Refresh(7, _ => { }, default));
        Assert.Null(assets.Error); Assert.NotNull(assets.Warning); Assert.Equal(oldAssets, assets.Json);
        var response = (await browser.GetFromJsonAsync<JsonObject>("/api/eve/characters/7/data"))!;
        Assert.Equal("Tritanium", response["inventory"]!["stock"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("Minerals", response["inventory"]!["stock"]![0]!["category"]!.GetValue<string>());
        Assert.Equal(30, response["inventory"]!["stock"]![0]!["quantity"]!.GetValue<int>());
        Assert.Equal(2, response["inventory"]!["blueprints"]!.AsArray().Count);
    }

    [Fact]
    public async Task Inventory_migration_preserves_existing_facts_and_management_notes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new GhostWatchDbContext(new DbContextOptionsBuilder<GhostWatchDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260922215018_AddEveFactualData");
        db.EveCharacters.Add(new() { CharacterId = 7, CharacterName = "Existing character" });
        db.EconomyTracks.Add(new() { Name = "Existing Track", Notes = "Keep this history" });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("INSERT INTO EveSections (CharacterId, Name, Json, AttemptedAt, UpdatedAt, Error) VALUES (7, 'wallet', '123.45', '2026-09-22 00:00:00', '2026-09-22 00:00:00', NULL)");
        await migrator.MigrateAsync();
        var wallet = await db.EveSections.SingleAsync();
        Assert.Equal("123.45", wallet.Json);
        Assert.Null(wallet.Warning);
        Assert.Equal("Keep this history", (await db.EconomyTracks.SingleAsync()).Notes);
        Assert.Empty(await db.PublicEveLookups.ToListAsync());
    }

    private static string Asset(int id, int quantity, long location = 60000001) =>
        $$"""{"item_id":{{id}},"type_id":100,"quantity":{{quantity}},"location_id":{{location}},"location_type":"station","location_flag":"Hangar"}""";
    private static string Blueprint(int id, int quantity, int runs) =>
        $$"""{"item_id":{{id}},"type_id":100,"quantity":{{quantity}},"location_id":60000001,"location_flag":"Hangar","material_efficiency":10,"time_efficiency":20,"runs":{{runs}}}""";
    private sealed class Tokens : ICharacterAccessTokens { public Task<string> Get(long id, CancellationToken ct) => Task.FromResult("test-token"); }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(response(request)); }
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
        { Content = new StringContent(body), Headers = { CacheControl = new CacheControlHeaderValue { NoStore = true } } };
}
