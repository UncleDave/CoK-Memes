using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot.Roster.Blizzard;

public sealed class BlizzardApiClient(HttpClient http, BlizzardRosterOptions options, TimeProvider clock, ILogger<BlizzardApiClient>? logger = null)
    : IDisposable
{
    private const int MaximumBodyBytes = 2 * 1024 * 1024;
    private const int MaximumCacheBytes = 16 * 1024 * 1024;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, CachedDocument> _cache = new(StringComparer.Ordinal);
    private int _cacheBytes;
    private string? _token;
    private DateTimeOffset _tokenExpires;
    private DateTimeOffset _blockedUntil;

    public async Task<BlizzardDocument> GetAsync(string path, string apiNamespace, CancellationToken cancellationToken)
    {
        if (!path.StartsWith("/data/wow/", StringComparison.Ordinal) && !path.StartsWith("/profile/wow/character/", StringComparison.Ordinal))
            throw new BlizzardApiException("Unexpected Blizzard resource path.");
        if (
            path.Contains('?')
            || path.Contains('#')
            || path.Contains("..", StringComparison.Ordinal)
            || path.Contains("//", StringComparison.Ordinal)
        )
            throw new BlizzardApiException("Unexpected Blizzard resource path.");
        if (apiNamespace != options.ProfileNamespace && apiNamespace != options.StaticNamespace)
            throw new BlizzardApiException("Unexpected Blizzard namespace.");
        var uri = new Uri($"https://{options.ApiHost}{path}?namespace={Uri.EscapeDataString(apiNamespace)}&locale=en_GB");
        var resourceKind =
            path.EndsWith("/professions", StringComparison.Ordinal) ? "professions"
            : path.EndsWith("/roster", StringComparison.Ordinal) ? "roster"
            : "static_data";
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = clock.GetUtcNow();
            if (now < _blockedUntil)
            {
                logger?.LogDebug("Blizzard {ResourceKind} request skipped; backed off until {BlockedUntilUtc}", resourceKind, _blockedUntil);
                throw new BlizzardApiException("Blizzard requests are temporarily backed off after an upstream failure.");
            }
            _cache.TryGetValue(uri.AbsoluteUri, out var cached);
            if (cached is not null && now < cached.Expires)
            {
                LogCache(resourceKind, "cache_hit", cached, null);
                return Document(cached);
            }

            for (var attempt = 0; attempt < 2; attempt++)
            {
                await EnsureTokenAsync(cancellationToken);
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                if (cached?.ETag is { } etag)
                    request.Headers.IfNoneMatch.Add(etag);
                if (cached?.LastModified is { } modified)
                    request.Headers.IfModifiedSince = modified;
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                now = clock.GetUtcNow();
                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                {
                    _token = null;
                    continue;
                }
                if (response.StatusCode == HttpStatusCode.NotModified && cached is not null)
                {
                    cached = cached with { Expires = Expires(response, now), ETag = response.Headers.ETag ?? cached.ETag };
                    if (response.Headers.CacheControl?.NoStore == true)
                    {
                        _cache.Remove(uri.AbsoluteUri);
                        _cacheBytes -= cached.Body.Length;
                    }
                    else
                        Store(uri.AbsoluteUri, cached);
                    LogCache(resourceKind, "not_modified", cached, response);
                    return Document(cached);
                }
                CheckStatus(response, "resource", resourceKind);
                var body = await ReadBodyAsync(response.Content, cancellationToken);
                // Validate JSON before accepting it into the HTTP cache. Shape validation belongs to the source.
                using var parsed = JsonDocument.Parse(body);
                var document = new CachedDocument(
                    body,
                    (response.Content.Headers.LastModified ?? now).UtcDateTime,
                    Expires(response, now),
                    response.Headers.ETag,
                    response.Content.Headers.LastModified
                );
                if (response.Headers.CacheControl?.NoStore != true)
                    Store(uri.AbsoluteUri, document);
                else if (_cache.Remove(uri.AbsoluteUri, out var removed))
                    _cacheBytes -= removed.Body.Length;
                LogCache(resourceKind, "fetched", document, response);
                return Document(document);
            }
            throw new BlizzardApiException("Blizzard authentication could not be renewed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (BlizzardApiException)
        {
            throw;
        }
        catch (Exception)
        {
            _blockedUntil = clock.GetUtcNow().AddSeconds(60);
            logger?.LogWarning("Blizzard {ResourceKind} transport or JSON failure; backed off until {BlockedUntilUtc}", resourceKind, _blockedUntil);
            throw new BlizzardApiException("Blizzard request failed or returned invalid diagnostic data; private response details were suppressed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && clock.GetUtcNow() < _tokenExpires)
            return;
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.battle.net/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}"))
        );
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        CheckStatus(response, "OAuth");
        using var document = JsonDocument.Parse(await ReadBodyAsync(response.Content, cancellationToken));
        var root = document.RootElement;
        if (
            root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("access_token", out var accessToken)
            || accessToken.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(accessToken.GetString())
            || !root.TryGetProperty("expires_in", out var lifetime)
            || !lifetime.TryGetInt32(out var seconds)
            || seconds <= 0
        )
            throw new BlizzardApiException("Blizzard OAuth response did not contain a usable token and expiry.");
        _token = accessToken.GetString();
        _tokenExpires = clock.GetUtcNow().AddSeconds(Math.Max(1, seconds - Math.Min(60, seconds / 10)));
    }

    private void CheckStatus(HttpResponseMessage response, string resource, string? resourceKind = null)
    {
        if (response.IsSuccessStatusCode)
            return;
        var now = clock.GetUtcNow();
        var retry = response.Headers.RetryAfter;
        var until = retry?.Date ?? (retry?.Delta is { } delta ? now.Add(delta) : now.AddSeconds(60));
        if (
            response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || (int)response.StatusCode >= 500
            || resource == "OAuth"
        )
            _blockedUntil = until > now ? until : now.AddSeconds(60);
        logger?.LogDebug(
            "Blizzard {ResourceKind} request failed with HTTP {StatusCode}; backed off until {BlockedUntilUtc}",
            resourceKind ?? resource,
            (int)response.StatusCode,
            _blockedUntil > now ? (DateTimeOffset?)_blockedUntil : null
        );
        throw new BlizzardApiException($"Blizzard {resource} request returned HTTP {(int)response.StatusCode}.", response.StatusCode, resource);
    }

    private void LogCache(string resourceKind, string outcome, CachedDocument document, HttpResponseMessage? response) =>
        logger?.LogDebug(
            "Blizzard {ResourceKind} {CacheOutcome}; source timestamp {SourceAtUtc}; cache expires {CacheExpiresUtc}; max-age {MaxAgeSeconds} s; Age {AgeSeconds} s",
            resourceKind,
            outcome,
            document.ObservedAtUtc,
            document.Expires,
            response?.Headers.CacheControl?.MaxAge?.TotalSeconds,
            response?.Headers.Age?.TotalSeconds
        );

    private static DateTimeOffset Expires(HttpResponseMessage response, DateTimeOffset now)
    {
        if (response.Headers.CacheControl?.NoCache == true)
            return now;
        var lifetime = response.Headers.CacheControl?.MaxAge ?? TimeSpan.FromSeconds(60);
        var remaining = lifetime - (response.Headers.Age ?? TimeSpan.Zero);
        return now.Add(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
    }

    private void Store(string key, CachedDocument document)
    {
        if (_cache.Remove(key, out var previous))
            _cacheBytes -= previous.Body.Length;
        if (_cacheBytes + document.Body.Length > MaximumCacheBytes || _cache.Count >= 256)
        {
            _cache.Clear();
            _cacheBytes = 0;
        }
        _cache[key] = document;
        _cacheBytes += document.Body.Length;
    }

    private static BlizzardDocument Document(CachedDocument cached)
    {
        using var document = JsonDocument.Parse(cached.Body);
        return new(document.RootElement.Clone(), cached.ObservedAtUtc);
    }

    private static async Task<byte[]> ReadBodyAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + read > MaximumBodyBytes)
                throw new BlizzardApiException("Blizzard response exceeded the 2 MiB size limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return output.ToArray();
    }

    public void Dispose()
    {
        http.Dispose();
        _gate.Dispose();
    }

    private sealed record CachedDocument(
        byte[] Body,
        DateTime ObservedAtUtc,
        DateTimeOffset Expires,
        EntityTagHeaderValue? ETag,
        DateTimeOffset? LastModified
    );
}
