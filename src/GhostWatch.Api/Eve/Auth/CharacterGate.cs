using System.Collections.Concurrent;

namespace GhostWatch.Api.Eve.Auth;

public sealed class CharacterGate
{
    private readonly ConcurrentDictionary<long, SemaphoreSlim> gates = new();
    public SemaphoreSlim For(long id) => gates.GetOrAdd(id, _ => new(1, 1));
}
