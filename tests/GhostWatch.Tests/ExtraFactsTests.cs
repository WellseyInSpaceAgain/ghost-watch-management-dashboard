using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Eve.Auth;
using GhostWatch.Api.Eve.Esi;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GhostWatch.Tests;

public class ExtraFactsTests
{
    [Fact]
    public async Task Additional_sections_are_named_without_changing_raw_payloads_and_failures_retain_facts()
    {
        var fail = false;
        using var handler = new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.StartsWith("/universe/"))
            {
                Assert.Null(request.Headers.Authorization);
                if (path.EndsWith("names"))
                {
                    var ids = JsonNode.Parse(await request.Content!.ReadAsStringAsync())!.AsArray();
                    return Json(new JsonArray(ids.Select(id => (JsonNode)new JsonObject { ["id"] = id!.DeepClone(), ["name"] = $"Resolved {id}" }).ToArray()).ToJsonString());
                }
                if (path.Contains("planets")) return Json("{\"name\":\"Jita IV\"}");
                if (path.Contains("types")) return Json("{\"name\":\"Science\",\"group_id\":270}");
                return Json("{\"name\":\"Science\",\"category_id\":16}");
            }
            Assert.Equal("Bearer fixture-token", request.Headers.Authorization!.ToString());
            if (path.EndsWith("wallet")) return Json("123.45");
            if (path.EndsWith("skills")) return Json("{\"skills\":[{\"skill_id\":3402,\"trained_skill_level\":5,\"active_skill_level\":3}]}");
            if (path.EndsWith("skillqueue")) return Json("[{\"skill_id\":3402,\"finished_level\":5}]");
            if (path.EndsWith("orders")) return Json("[{\"type_id\":34,\"location_id\":60000001,\"price\":4.5,\"volume_remain\":20,\"is_buy_order\":true}]");
            if (path.EndsWith("standings")) return Json("[{\"from_id\":500001,\"from_type\":\"faction\",\"standing\":4.1}]");
            if (path.EndsWith("points")) return Json("[{\"corporation_id\":100001,\"loyalty_points\":2500}]");
            if (path.EndsWith("planets")) return fail ? new(HttpStatusCode.Forbidden) : Json("[{\"planet_id\":40000001,\"solar_system_id\":30000001,\"num_pins\":12,\"planet_type\":\"temperate\"}]");
            return Json("[]");
        });
        await using var app = new TestApplication(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<ICharacterAccessTokens, Tokens>();
            services.AddHttpClient<EsiClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        }));
        using var browser = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
        db.EveCharacters.Add(new() { CharacterId = 7, CharacterName = "Economic pilot", GrantedScopesJson = EveScopes.Store(EveScopes.Required) });
        var account = new GhostWatch.Api.Management.ManagedAccount { Name = "Main", Subscription = "Alpha" };
        db.ManagedAccounts.Add(account);
        db.CharacterPlans.Add(new() { CharacterId = 7, AccountId = account.Id, Assignment = "Manual label" });
        await db.SaveChangesAsync();
        var refresh = scope.ServiceProvider.GetRequiredService<CharacterRefresh>();
        Assert.True(await refresh.Refresh(7, _ => { }, default));
        var planet = await db.EveSections.SingleAsync(x => x.Name == "planets");
        var oldJson = planet.Json; var oldTime = planet.UpdatedAt;
        Assert.DoesNotContain("Jita", oldJson);
        var result = (await browser.GetFromJsonAsync<JsonObject>("/api/eve/characters/7/data"))!;
        Assert.Equal("Jita IV", result["facts"]!["planets"]![0]!["planet_name"]!.GetValue<string>());
        Assert.Equal("Resolved 500001", result["facts"]!["standings"]![0]!["from_name"]!.GetValue<string>());
        Assert.Equal("Resolved 100001", result["facts"]!["loyalty"]![0]!["corporation_name"]!.GetValue<string>());
        Assert.Equal(90m, result["facts"]!["marketOrders"]![0]!["remaining_value"]!.GetValue<decimal>());
        Assert.Equal("Science", result["facts"]!["skills"]!["skills"]![0]!["skill_name"]!.GetValue<string>());
        Assert.Contains("Alpha", result["assessment"]!["accountState"]!.GetValue<string>());
        Assert.Single(result["assessment"]!["dormantSkills"]!.AsArray());
        fail = true;
        Assert.False(await refresh.Refresh(7, _ => { }, default));
        Assert.Equal(oldJson, planet.Json); Assert.Equal(oldTime, planet.UpdatedAt); Assert.NotNull(planet.Error);
        Assert.Equal("Manual label", (await db.CharacterPlans.SingleAsync()).Assignment);
    }

    [Fact]
    public void Foundations_never_claim_recipe_eligibility_or_invent_missing_active_levels()
    {
        var skills = JsonNode.Parse("{\"skills\":[{\"skill_id\":3402,\"trained_skill_level\":5,\"skill_name\":\"Science\"},{\"skill_id\":3406,\"trained_skill_level\":5},{\"skill_id\":21790,\"trained_skill_level\":3},{\"skill_id\":21791,\"trained_skill_level\":3}]}")!;
        var capacity = CapacityCalculator.Calculate(skills)!;
        var assessment = EconomicAssessor.Assess(skills, capacity, null, null);
        Assert.Null(capacity.Active);
        Assert.Contains("Strong trained foundation", assessment.Areas.Single(x => x.Key == "invention").Summary);
        Assert.All(assessment.ProductionReadiness, row => Assert.Equal("notVerified", row.Status));
        Assert.Empty(assessment.DormantSkills);
        Assert.All(assessment.Areas.SelectMany(x => x.Evidence), row => Assert.Null(row.ActiveLevel));
    }
    private sealed class Tokens : ICharacterAccessTokens { public Task<string> Get(long id, CancellationToken ct) => Task.FromResult("fixture-token"); }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => response(request); }
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body), Headers = { CacheControl = new CacheControlHeaderValue { NoStore = true } } };
}
