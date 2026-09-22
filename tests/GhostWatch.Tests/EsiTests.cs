using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Economics.Tracks;
using GhostWatch.Api.Eve;
using GhostWatch.Api.Eve.Auth;
using GhostWatch.Api.Eve.Esi;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace GhostWatch.Tests;

public class EsiTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(response(request));
    }
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
        { Content = new StringContent(body), Headers = { CacheControl = new CacheControlHeaderValue { NoStore = true } } };
    private sealed class Tokens : ICharacterAccessTokens
    {
        public Task<string> Get(long id, CancellationToken ct) => Task.FromResult($"test-token-{id}");
    }

    [Fact]
    public async Task Pagination_reads_every_page_and_uses_bearer_authorisation()
    {
        var paths = new List<string>();
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal("Bearer private-token", request.Headers.Authorization!.ToString());
            paths.Add(request.RequestUri!.PathAndQuery);
            var response = Json(request.RequestUri.Query.Contains("page=1") ? "[{\"item_id\":1}]" : "[{\"item_id\":2}]");
            response.Headers.Add("X-Pages", "2");
            return response;
        })) { BaseAddress = new("https://esi.evetech.net/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var esi = new EsiClient(http, new(), cache);
        var rows = await esi.Pages("characters/7/assets", "private-token", 7, default);
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "/characters/7/assets?page=1", "/characters/7/assets?page=2" }, paths);
        await Assert.ThrowsAsync<ArgumentException>(() => esi.Get("https://attacker.invalid/", "private-token", default));
    }

    [Fact]
    public async Task Failed_later_page_never_returns_a_partial_collection()
    {
        using var http = new HttpClient(new Handler(request =>
        {
            if (request.RequestUri!.Query.Contains("page=2")) return new(HttpStatusCode.Forbidden);
            var response = Json("[{\"item_id\":1}]"); response.Headers.Add("X-Pages", "2"); return response;
        })) { BaseAddress = new("https://esi.evetech.net/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        await Assert.ThrowsAsync<EsiException>(() => new EsiClient(http, new(), cache).Pages("characters/7/assets", "token", 7, default));
    }

    [Fact]
    public async Task Private_cache_is_partitioned_by_character_and_authorisation()
    {
        var requests = 0;
        using var http = new HttpClient(new Handler(_ =>
        {
            requests++;
            var response = Json("100.5");
            response.Headers.CacheControl = new() { MaxAge = TimeSpan.FromMinutes(1) };
            return response;
        })) { BaseAddress = new("https://esi.evetech.net/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var esi = new EsiClient(http, new(), cache);
        await esi.Get("characters/7/wallet", "first", default, 7);
        await esi.Get("characters/7/wallet", "first", default, 7);
        Assert.Equal(1, requests);
        await esi.Get("characters/7/wallet", "second", default, 7);
        await esi.Get("characters/7/wallet", "second", default, 8);
        Assert.Equal(3, requests);
    }

    [Fact]
    public async Task Refresh_preserves_previous_data_and_local_notes_and_updates_stable_jobs()
    {
        var stage = 0;
        using var upstream = new Handler(request =>
        {
            Assert.Equal("Bearer test-token-7", request.Headers.Authorization!.ToString());
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("wallet")) return stage == 1 ? new(HttpStatusCode.Forbidden) : Json(stage == 0 ? "123456.78" : "200000");
            if (path.EndsWith("skills")) return Json("{\"skills\":[{\"skill_id\":3387,\"trained_skill_level\":5,\"active_skill_level\":2}],\"total_sp\":1234}");
            if (path.EndsWith("jobs")) return Json(stage == 3 ? "[]" : stage == 2 ? "[{\"job_id\":42}]" : "[{\"job_id\":42,\"activity_id\":1,\"blueprint_type_id\":123,\"product_type_id\":456,\"runs\":10,\"status\":\"" + (stage == 0 ? "active" : "delivered") + "\",\"start_date\":\"2026-09-01T00:00:00Z\",\"end_date\":\"2026-09-02T00:00:00Z\"}]");
            return Json("[]");
        });
        await using var app = new TestApplication(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<ICharacterAccessTokens, Tokens>();
            services.AddHttpClient<EsiClient>().ConfigurePrimaryHttpMessageHandler(() => upstream);
        }));
        using var browser = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
        db.EveCharacters.Add(new() { CharacterId = 7, CharacterName = "Test pilot" });
        var track = new EconomyTrack { Name = "Local strategy", Notes = "Do not overwrite", Status = "Active" };
        db.EconomyTracks.Add(track); await db.SaveChangesAsync();
        var refresh = scope.ServiceProvider.GetRequiredService<CharacterRefresh>();
        Assert.True(await refresh.Refresh(7, _ => { }, default));
        var wallet = await db.EveSections.SingleAsync(x => x.Name == "wallet");
        var successAt = wallet.UpdatedAt;
        Assert.Equal("123456.78", wallet.Json);
        stage = 1;
        Assert.False(await refresh.Refresh(7, _ => { }, default));
        Assert.Equal("123456.78", wallet.Json);
        Assert.Equal(successAt, wallet.UpdatedAt);
        Assert.NotNull(wallet.Error);
        Assert.Equal("delivered", (await db.EveIndustryJobs.SingleAsync()).Status);
        Assert.Equal("Do not overwrite", (await db.EconomyTracks.SingleAsync()).Notes);
        stage = 2;
        Assert.False(await refresh.Refresh(7, _ => { }, default));
        Assert.Equal("delivered", (await db.EveIndustryJobs.SingleAsync()).Status);
        Assert.NotNull((await db.EveSections.SingleAsync(x => x.Name == "industryJobs")).Error);
        stage = 3;
        Assert.True(await refresh.Refresh(7, _ => { }, default));
        Assert.Null(wallet.Error);
        Assert.Equal("200000", wallet.Json);
        Assert.Single(await db.EveIndustryJobs.ToListAsync()); // ESI lookback expiry does not delete history.
        var result = await browser.GetFromJsonAsync<JsonObject>("/api/eve/characters/7/data");
        Assert.Equal(6, result!["capacity"]!["trained"]!["manufacturingJobs"]!.GetValue<int>());
        Assert.Equal(3, result["capacity"]!["active"]!["manufacturingJobs"]!.GetValue<int>());
        Assert.DoesNotContain("test-token", result.ToJsonString());
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.PostAsync("/api/eve/characters/7/refresh", null)).StatusCode);
    }

    [Fact]
    public void Missing_active_skills_stay_unknown_and_trained_capacity_is_distinct()
    {
        Assert.Null(CapacityCalculator.Calculate(null));
        var skills = JsonNode.Parse("{\"skills\":[{\"skill_id\":3387,\"trained_skill_level\":5}]}")!;
        var capacity = CapacityCalculator.Calculate(skills)!;
        Assert.Equal(6, capacity.Trained.ManufacturingJobs);
        Assert.Null(capacity.Active);
        Assert.ThrowsAny<Exception>(() => CharacterRefresh.Validate("skills", JsonNode.Parse("{\"skills\":[{\"skill_id\":3387,\"trained_skill_level\":7}]}")!));
    }

    [Fact]
    public async Task Queue_refreshes_multiple_characters_without_mixing_facts()
    {
        using var upstream = new Handler(request => request.RequestUri!.AbsolutePath.EndsWith("wallet")
            ? Json(request.RequestUri.AbsolutePath.Contains("/7/") ? "700" : "800")
            : request.RequestUri.AbsolutePath.EndsWith("skills") ? Json("{\"skills\":[]}") : Json("[]"));
        await using var app = new TestApplication(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<ICharacterAccessTokens, Tokens>();
            services.AddHttpClient<EsiClient>().ConfigurePrimaryHttpMessageHandler(() => upstream);
        }));
        using var browser = app.CreateClient();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
            db.EveCharacters.AddRange(new EveCharacter { CharacterId = 7, CharacterName = "First" }, new EveCharacter { CharacterId = 8, CharacterName = "Second" });
            await db.SaveChangesAsync();
        }
        browser.DefaultRequestHeaders.Add("X-Ghost-Watch", "1");
        Assert.Equal(HttpStatusCode.NotFound, (await browser.PostAsync("/api/eve/characters/99/refresh", null)).StatusCode);
        foreach (var id in new[] { 7, 8 })
            Assert.Equal(HttpStatusCode.Accepted, (await browser.PostAsync($"/api/eve/characters/{id}/refresh", null)).StatusCode);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        foreach (var id in new[] { 7, 8 })
        {
            JsonObject result;
            do
            {
                await Task.Delay(20, deadline.Token);
                result = (await browser.GetFromJsonAsync<JsonObject>($"/api/eve/characters/{id}/data", deadline.Token))!;
            } while (result["progress"]!["state"]!.GetValue<string>() is "queued" or "running");
            Assert.Equal("complete", result["progress"]!["state"]!.GetValue<string>());
            var wallet = result["sections"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == "wallet")!;
            Assert.Equal(id * 100, wallet["data"]!.GetValue<int>());
        }
    }

    [Fact]
    public async Task Retry_recovers_from_server_error_and_shared_cooldown_honours_cancellation()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(_ => ++calls == 1 ? new(HttpStatusCode.ServiceUnavailable) : Json("42")))
            { BaseAddress = new("https://esi.evetech.net/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var throttle = new EsiThrottle();
        var client = new EsiClient(http, throttle, cache);
        Assert.Equal(42, (await client.Get("characters/7/wallet", "token", default)).GetValue<int>());
        Assert.Equal(2, calls);
        throttle.Pause(TimeSpan.FromMinutes(1));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Get("characters/8/wallet", "token", cancellation.Token));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Duplicate_refreshes_are_rejected_while_queued()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var queue = new RefreshQueue(services.GetRequiredService<IServiceScopeFactory>());
        Assert.True(queue.Enqueue(7));
        Assert.False(queue.Enqueue(7));
        Assert.True(queue.Enqueue(8));
        Assert.Equal("queued", queue.Status(7).State);
    }
}
