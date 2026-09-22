using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace GhostWatch.Api.Eve.Esi;

public class EsiException(int status) : Exception(status switch
{
    401 => "Authorization expired. Reconnect the character.",
    403 => "Access denied or scope missing. Reconnect to grant access.",
    404 => "ESI data is unavailable or the location is inaccessible.",
    420 or 429 => "ESI rate limit reached. Try again after the cooldown.",
    _ => "ESI is temporarily unavailable. Try again later."
}) { public int Status { get; } = status; }

// Shared across clients and characters so an error budget cooldown is respected globally.
public class EsiThrottle
{
    private long until;
    public void Pause(TimeSpan duration)
    {
        var next = DateTimeOffset.UtcNow.Add(duration).UtcTicks;
        long current;
        do { current = Interlocked.Read(ref until); if (current >= next) return; }
        while (Interlocked.CompareExchange(ref until, next, current) != current);
    }
    public async Task Wait(CancellationToken ct)
    {
        while (new DateTimeOffset(Interlocked.Read(ref until), TimeSpan.Zero) - DateTimeOffset.UtcNow is var delay && delay > TimeSpan.Zero)
            await Task.Delay(delay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : delay, ct);
    }
}

public class EsiClient(HttpClient http, EsiThrottle throttle, IMemoryCache cache)
{
    private record CachedResponse(string Json, int Pages, DateTimeOffset Expires);
    private static int Header(HttpResponseMessage r, string name, int fallback) =>
        r.Headers.TryGetValues(name, out var values) && int.TryParse(values.FirstOrDefault(), out var n) ? n : fallback;

    public async Task<(JsonNode Data, int Pages)> Request(string path, string? token, CancellationToken ct, JsonNode? body = null, long? characterId = null)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out _) || path.StartsWith('/') || path.Contains(".."))
            throw new ArgumentException("ESI requests must use a relative application-owned path.", nameof(path));
        var authorization = token is null ? "public" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var key = $"esi:{characterId}:{authorization}:{path}:{body?.ToJsonString()}";
        if (cache.TryGetValue<CachedResponse>(key, out var cached) && cached is not null && cached.Expires > DateTimeOffset.UtcNow)
            return (JsonNode.Parse(cached.Json)!, cached.Pages);
        for (var attempt = 0; ; attempt++)
        {
            await throttle.Wait(ct);
            using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, path);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            HttpResponseMessage response;
            try { response = await http.SendAsync(request, ct); }
            catch (HttpRequestException) when (attempt < 2) { await Task.Delay(TimeSpan.FromSeconds(2 << attempt), ct); continue; }
            using (response)
            {
                var errorRemaining = Header(response, "X-Esi-Error-Limit-Remain", 100);
                if (errorRemaining < 10) throttle.Pause(TimeSpan.FromSeconds(Header(response, "X-Esi-Error-Limit-Reset", 60)));
                var status = (int)response.StatusCode;
                if (status is 420 or 429)
                {
                    var delay = response.Headers.RetryAfter?.Delta ??
                        (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromSeconds(60);
                    throttle.Pause(delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1));
                }
                if ((status is 420 or 429 || status >= 500) && attempt < 2)
                { await Task.Delay(TimeSpan.FromSeconds(2 << attempt), ct); continue; }
                if (!response.IsSuccessStatusCode) throw new EsiException(status);
                var json = await response.Content.ReadAsStringAsync(ct);
                var pages = Math.Max(1, Header(response, "X-Pages", 1));
                var ttl = response.Headers.CacheControl?.MaxAge ?? TimeSpan.FromSeconds(30);
                ttl -= response.Headers.Age ?? TimeSpan.Zero;
                if (ttl > TimeSpan.Zero && response.Headers.CacheControl?.NoStore != true && response.Headers.CacheControl?.NoCache != true)
                    cache.Set(key, new CachedResponse(json, pages, DateTimeOffset.UtcNow.Add(ttl)), ttl);
                return (JsonNode.Parse(json)!, pages);
            }
        }
    }

    public async Task<JsonNode> Get(string path, string? token, CancellationToken ct, long? characterId = null) =>
        (await Request(path, token, ct, characterId: characterId)).Data;

    public async Task<JsonArray> Pages(string path, string token, long characterId, CancellationToken ct)
    {
        // Build the replacement in memory; a later page failure preserves the previous complete section.
        var separator = path.Contains('?') ? '&' : '?';
        var (first, pages) = await Request($"{path}{separator}page=1", token, ct, characterId: characterId);
        var result = first.AsArray();
        for (var page = 2; page <= pages; page++)
        {
            var next = await Get($"{path}{separator}page={page}", token, ct, characterId);
            foreach (var row in next.AsArray()) result.Add(row?.DeepClone());
        }
        return result;
    }
}
