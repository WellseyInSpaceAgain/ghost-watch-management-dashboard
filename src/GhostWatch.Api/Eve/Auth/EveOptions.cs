namespace GhostWatch.Api.Eve.Auth;

public sealed class EveOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string CallbackUrl { get; set; } = "http://localhost:8080/api/auth/eve/callback";
    public bool Configured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret)
        && Uri.TryCreate(CallbackUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback)
        && uri.AbsolutePath == "/api/auth/eve/callback" && uri.Query == "" && uri.Fragment == "" && uri.UserInfo == "";

    // Scopes for the character economic data required by the brief; no corporation access.
    public static readonly string[] Scopes = [
        "esi-skills.read_skills.v1", "esi-skills.read_skillqueue.v1",
        "esi-industry.read_character_jobs.v1", "esi-characters.read_blueprints.v1",
        "esi-assets.read_assets.v1", "esi-wallet.read_character_wallet.v1",
        "esi-markets.read_character_orders.v1", "esi-characters.read_standings.v1",
        "esi-characters.read_loyalty.v1", "esi-planets.manage_planets.v1"
    ];
}
