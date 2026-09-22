using System.Text.Json.Nodes;

namespace GhostWatch.Api.Eve.Esi;

public sealed record SkillCapacity(int ManufacturingJobs, int ResearchJobs, int ReactionJobs, int MarketOrders, int PiColonies);
public sealed record EconomicCapacity(SkillCapacity Trained, SkillCapacity? Active);

public static class CapacityCalculator
{
    // Adapted from the reference calculator. These are broad skill capacities, not recipe eligibility.
    public static EconomicCapacity? Calculate(JsonNode? skills)
    {
        if (skills?["skills"] is not JsonArray rows) return null;
        int Level(long id, string field) => rows.FirstOrDefault(s => s?["skill_id"]?.GetValue<long>() == id)?[field]?.GetValue<int>() ?? 0;
        SkillCapacity From(string field) => new(
            1 + Level(3387, field) + Level(24625, field),
            1 + Level(3406, field) + Level(24624, field),
            Level(45746, field) > 0 ? 1 + Level(45748, field) + Level(45749, field) : 0,
            5 + 4 * Level(3443, field) + 8 * Level(3444, field) + 16 * Level(16596, field) + 32 * Level(18580, field),
            1 + Level(2495, field));
        return new(From("trained_skill_level"), rows.All(s => s?["active_skill_level"] is not null) ? From("active_skill_level") : null);
    }
}
