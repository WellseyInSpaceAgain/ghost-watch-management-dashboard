using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Economics;
using GhostWatch.Api.Economics.Capital;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace GhostWatch.Tests;
public class CapitalTests
{
    [Fact]
    public async Task Adjustments_transfer_atomically_keep_history_and_warn_without_changing_wallets()
    {
        await using var app = new TestApplication(); using var browser = app.CreateClient();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
            db.EveCharacters.Add(new() { CharacterId = 7, CharacterName = "Wallet pilot" });
            db.EveSections.Add(new() { CharacterId = 7, Name = "wallet", Json = "900", UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        async Task<CapitalPool> Pool(string name) => (await (await browser.PostAsJsonAsync("/api/economics/capital/pools", new PoolInput(name, "", "Other", null))).Content.ReadFromJsonAsync<CapitalPool>())!;
        var core = await Pool("Core"); var treasury = await Pool("Treasury");
        (await browser.PostAsJsonAsync("/api/economics/capital/adjustments", new AdjustmentInput(null, core.Id, 1200, "Initial allocation", "Conceptual"))).EnsureSuccessStatusCode();
        (await browser.PostAsJsonAsync("/api/economics/capital/adjustments", new AdjustmentInput(core.Id, treasury.Id, 250, "Treasury transfer", ""))).EnsureSuccessStatusCode();
        var summary = (await browser.GetFromJsonAsync<JsonObject>("/api/economics/capital"))!;
        Assert.Equal(1200, summary["allocated"]!.GetValue<decimal>());
        Assert.Equal(300, summary["overAllocated"]!.GetValue<decimal>());
        Assert.Equal(2, summary["adjustments"]!.AsArray().Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsJsonAsync("/api/economics/capital/adjustments", new AdjustmentInput(core.Id, treasury.Id, 1000, "Too much", ""))).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>();
            Assert.Equal(950, (await db.CapitalPools.FindAsync(core.Id))!.AllocatedCapital);
            Assert.Equal(250, (await db.CapitalPools.FindAsync(treasury.Id))!.AllocatedCapital);
            Assert.Equal("900", (await db.EveSections.SingleAsync()).Json);
            Assert.Equal(2, await db.CapitalAdjustments.CountAsync());
            db.EveCharacters.Add(new() { CharacterId = 8, CharacterName = "Uncollected" }); await db.SaveChangesAsync();
        }
        summary = (await browser.GetFromJsonAsync<JsonObject>("/api/economics/capital"))!;
        Assert.Null(summary["liquid"]); Assert.Null(summary["overAllocated"]); Assert.Equal(900, summary["knownLiquid"]!.GetValue<decimal>());
        Assert.Equal(HttpStatusCode.Conflict, (await browser.PutAsJsonAsync($"/api/economics/capital/pools/{core.Id}", new PoolInput("Core", "", "Other", null, false, 1))).StatusCode);
    }
    [Fact]
    public void Financial_unknowns_do_not_become_zero()
    {
        Assert.Null(FinancialMath.Profit(10, 0, null, 20));
        Assert.Equal(7, FinancialMath.Profit(10, 0, 3, 20));
        Assert.Null(FinancialMath.CompleteSum([10, null]));
        Assert.Equal(0, FinancialMath.CompleteSum([]));
        Assert.Null(FinancialMath.Coverage(100, 0));
        Assert.Equal(3, FinancialMath.Coverage(2400, 800));
        Assert.Null(FinancialMath.SlotDays(null, 1));
        Assert.Equal(2, FinancialMath.SlotDays(24, 2));
    }
}
