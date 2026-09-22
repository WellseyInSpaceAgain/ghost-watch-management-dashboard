using System.Text.Json.Nodes;

namespace GhostWatch.Api.Eve.Esi;

/// <summary>Descriptive trained-skill evidence, never a blueprint eligibility checker or a role assignment.</summary>
public static class EconomicAssessor
{
    private static readonly long[] Manufacturing = [3380, 3388, 3387, 24625];
    private static readonly long[] Research = [3406, 24624, 3403, 3409, 3402, 24270];
    private static readonly long[] Reactions = [45746, 45748, 45749, 45750];
    private static readonly long[] Pi = [2495, 2505, 2406, 2403, 13279, 33467];
    private static readonly long[] Trading = [3443, 3444, 16596, 18580, 16622, 3446, 16597, 16598, 16594, 16595];
    private static readonly long[] Encryption = [21790, 21791, 23087, 23121, 52308];

    public static EconomicAssessment Assess(JsonNode skills, EconomicCapacity capacity, bool? isOmega, int? colonies)
    {
        var rows = skills["skills"].Rows().ToArray();
        SkillEvidence Evidence(JsonNode s) => new(s.Long("skill_id"), s.Text("skill_name", $"Skill {s.Long("skill_id")}"),
            (int)s.Long("trained_skill_level"), s["active_skill_level"] is null ? null : (int)s.Long("active_skill_level"));
        List<SkillEvidence> Select(long[] ids) => ids.SelectMany(id => rows.Where(s => s.Long("skill_id") == id)).Select(Evidence).ToList();
        int Level(long id) => (int)rows.FirstOrDefault(s => s.Long("skill_id") == id).Long("trained_skill_level");
        var cap = capacity.Trained;
        var encryption = Select(Encryption).Where(s => s.TrainedLevel > 0).ToList();
        var science = rows.Where(s => s.Text("group_name") == "Science" && !Research.Contains(s.Long("skill_id")) && !Encryption.Contains(s.Long("skill_id")))
            .Select(Evidence).Where(s => s.TrainedLevel > 0).OrderByDescending(s => s.TrainedLevel).ThenBy(s => s.Name).ToList();
        // "Strong" describes a foundation only: Science IV+, 6+ slots and at least two encryption methods III+.
        var strongInvention = Level(3402) >= 4 && cap.ResearchJobs >= 6 && encryption.Count(s => s.TrainedLevel >= 3) >= 2;
        var invention = strongInvention ? "Strong trained foundation" : encryption.Count > 0 ? "Some trained encryption foundation" : "Limited trained foundation; no empire/Triglavian encryption methods trained";
        var areas = new List<EconomicArea>
        {
            new("manufacturing", "Manufacturing", cap.ManufacturingJobs == 1 && Level(3380) <= 1 && Level(3388) == 0
                ? "Basic/default capacity only" : $"{cap.ManufacturingJobs} trained job slots", cap.ManufacturingJobs >= 6 || Level(3380) >= 4, Select(Manufacturing)),
            new("research", strongInvention ? "Research / Invention" : "Research", $"{cap.ResearchJobs} trained science slots" + (strongInvention ? "; strong trained invention foundation" : "") + (Level(3409) >= 4 ? $"; Metallurgy {Roman(Level(3409))}" : ""),
                cap.ResearchJobs >= 6 || Level(3409) >= 4, Select(Research)),
            new("invention", "Invention foundation", $"{invention}; {encryption.Count} encryption methods trained. Individual blueprint eligibility not verified.",
                false, Select([3402]).Concat(encryption).Concat(science).ToList()),
            new("reactions", "Reactions", cap.ReactionJobs == 0 ? "No trained reaction capacity" : $"{cap.ReactionJobs} trained reaction job slots", cap.ReactionJobs > 0, Select(Reactions)),
            new("pi", "Planetary Interaction", $"{cap.PiColonies}-colony trained potential; {colonies?.ToString() ?? "unknown"} colonies currently deployed", Level(2495) >= 3 || Level(2505) >= 4, Select(Pi)),
            new("trading", "Trading", $"{cap.MarketOrders} trained order capacity", cap.MarketOrders >= 25 || Level(16622) >= 4 || Level(3446) >= 4, Select(Trading))
        };
        var refining = rows.Where(s => s.Long("skill_id") is 3385 or 3389 ||
            (s.Text("group_name") == "Resource Processing" && s.Text("skill_name").EndsWith("Processing", StringComparison.Ordinal)))
            .Select(Evidence).Where(s => s.TrainedLevel > 0).OrderByDescending(s => s.TrainedLevel).ThenBy(s => s.Name).ToList();
        if (refining.Count > 0) areas.Add(new("refining", "Refining", "Trained refining skills; yield depends on material and facility", refining.Any(s => s.TrainedLevel >= 4), refining));
        var logistics = rows.Where(s => s.Text("skill_name").Contains("Hauler", StringComparison.Ordinal) || s.Text("skill_name").Contains("Freighter", StringComparison.Ordinal) || s.Text("skill_name") == "Transport Ships")
            .Select(Evidence).Where(s => s.TrainedLevel > 0).OrderByDescending(s => s.TrainedLevel).ThenBy(s => s.Name).ToList();
        if (logistics.Count > 0) areas.Add(new("logistics", "Hauling / Logistics", "Trained transport skills; ship and fitting requirements still apply", logistics.Any(s => s.TrainedLevel >= 4), logistics));
        long[] capacitySkills = [24625, 24624, 3444, 2495, 2505, 3387, 3406, 16596, 18580, 3443];
        var dormant = areas.SelectMany(a => a.Evidence).DistinctBy(s => s.SkillId).Where(s => s.ActiveLevel < s.TrainedLevel)
            .OrderBy(s => Array.IndexOf(capacitySkills, s.SkillId) is var index && index >= 0 ? index : capacitySkills.Length).ToList();
        var piDormant = areas.Single(a => a.Key == "pi").Evidence.Any(s => s.ActiveLevel < s.TrainedLevel);
        return new(areas,
            [new("t2", "T2 manufacturing", "notVerified", "Recipe-specific; not verified"),
             new("t3Invention", "T3 invention", "notVerified", "Recipe-specific; not verified"),
             new("t3Components", "T3 component manufacturing", "notVerified", "Recipe-specific; not verified"),
             new("t3Assembly", "T3 final assembly", "notVerified", "Recipe-specific; not verified")],
            dormant, isOmega is null ? "Unknown (account not assigned)" : isOmega.Value ? "Omega (user-set)" : "Alpha (user-set)",
            isOmega == false ? "Alpha restricted: colony exports unavailable." + (piDormant ? " Omega-trained PI skill levels are inactive." : "")
                : isOmega == true ? "Omega (user-set); skill potential does not verify colony or facility access."
                : "Account state unknown; PI availability not verified.");
    }

    public static string Roman(int level) => level switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => level.ToString() };
    public static string Describe(SkillEvidence s) => $"{s.Name} {Roman(s.TrainedLevel)}" +
        (s.ActiveLevel is null ? " (active unknown)" : s.ActiveLevel != s.TrainedLevel ? $" (active {Roman(s.ActiveLevel.Value)})" : "");
}
