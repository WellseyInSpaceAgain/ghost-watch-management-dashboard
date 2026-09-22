using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace GhostWatch.Api.Eve.Auth;

public sealed record PendingLogin(string Verifier, string Browser, long? CharacterId = null);

public sealed class PendingLogins(IMemoryCache cache)
{
    private readonly object sync = new();
    public void Add(string state, PendingLogin login) => cache.Set($"oauth:{state}", login, TimeSpan.FromMinutes(10));

    // Check browser binding and consume atomically, including concurrent callbacks.
    public PendingLogin? Consume(string? state, string? browser)
    {
        if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(browser)) return null;
        lock (sync)
        {
            if (!cache.TryGetValue<PendingLogin>($"oauth:{state}", out var pending) || pending is null ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(browser), Encoding.UTF8.GetBytes(pending.Browser)))
                return null;
            cache.Remove($"oauth:{state}");
            return pending;
        }
    }
}
