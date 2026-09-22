using System.Collections.Concurrent;
using System.Threading.Channels;

namespace GhostWatch.Api.Eve.Esi;

public sealed record RefreshProgress(string State, string? Section = null, string? Error = null);

public sealed class RefreshQueue(IServiceScopeFactory scopes) : BackgroundService
{
    private readonly Channel<long> queue = Channel.CreateUnbounded<long>(new() { SingleReader = true });
    private readonly ConcurrentDictionary<long, RefreshProgress> progress = new();
    private readonly object sync = new();
    public RefreshProgress Status(long id) => progress.GetValueOrDefault(id, new("idle"));
    public bool Enqueue(long id)
    {
        lock (sync)
        {
            if (Status(id).State is "queued" or "running") return false;
            progress[id] = new("queued");
            return queue.Writer.TryWrite(id);
        }
    }
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var id in queue.Reader.ReadAllAsync(ct))
            {
                using var scope = scopes.CreateScope();
                try
                {
                    var complete = await scope.ServiceProvider.GetRequiredService<CharacterRefresh>().Refresh(id,
                        section => progress[id] = new("running", section), ct);
                    progress[id] = new(complete ? "complete" : "partial");
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception error) { progress[id] = new("failed", Error: CharacterRefresh.SafeError(error)); }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }
}
