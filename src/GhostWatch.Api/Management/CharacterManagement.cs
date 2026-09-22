using GhostWatch.Api.Economics.Tracks;
using GhostWatch.Api.Eve;

namespace GhostWatch.Api.Management;

// User-maintained planning. SSO and ESI never mutate these tables.
public sealed class ManagedAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Subscription { get; set; } = "Unknown";
    public string Notes { get; set; } = "";
    public int Revision { get; set; } = 1;
}
public sealed class CharacterPlan
{
    public long CharacterId { get; set; }
    public Guid? AccountId { get; set; }
    public ManagedAccount? Account { get; set; }
    public string Assignment { get; set; } = "";
    public string Notes { get; set; } = "";
    public int Revision { get; set; } = 1;
}
public sealed class CharacterTrack
{
    public long CharacterId { get; set; }
    public Guid TrackId { get; set; }
    public EconomyTrack Track { get; set; } = null!;
    public EveCharacter Character { get; set; } = null!;
}
