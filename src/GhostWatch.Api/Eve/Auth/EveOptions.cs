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

    public static IEnumerable<string> Scopes => EveScopes.Required;
}
