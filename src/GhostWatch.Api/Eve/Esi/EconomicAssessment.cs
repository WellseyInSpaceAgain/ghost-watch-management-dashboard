using System.Text.Json.Nodes;
namespace GhostWatch.Api.Eve.Esi;

public sealed record SkillEvidence(long SkillId, string Name, int TrainedLevel, int? ActiveLevel);
public sealed record EconomicArea(string Key, string Label, string Summary, bool IsStrength, List<SkillEvidence> Evidence);
public sealed record ProductionReadiness(string Key, string Label, string Status, string Summary);
public sealed record EconomicAssessment(List<EconomicArea> Areas, List<ProductionReadiness> ProductionReadiness,
    List<SkillEvidence> DormantSkills, string AccountState, string PiAvailability);

internal static class SkillJson
{
    public static IEnumerable<JsonNode> Rows(this JsonNode? value) => value is JsonArray a ? a.OfType<JsonNode>() : [];
    public static long Long(this JsonNode? value, string key) => value?[key]?.GetValue<long>() ?? 0;
    public static string Text(this JsonNode? value, string key, string fallback = "") => value?[key]?.GetValue<string>() ?? fallback;
}
