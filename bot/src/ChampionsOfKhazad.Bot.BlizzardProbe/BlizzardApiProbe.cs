using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ChampionsOfKhazad.Bot.BlizzardProbe;

public sealed class BlizzardApiProbe(HttpClient http, TextWriter output)
{
    private const int MaximumResponseBytes = 2 * 1024 * 1024;

    public async Task<int> RunAsync(ProbeOptions options, string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.battle.net/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await output.WriteLineAsync($"OAuth: HTTP {(int)response.StatusCode}");
        if (!response.IsSuccessStatusCode)
            return 1;

        var body = await ReadBodyAsync(response.Content, cancellationToken);
        if (body is null)
        {
            await output.WriteLineAsync("OAuth response exceeded the diagnostic size limit.");
            return 1;
        }
        using var tokenDocument = JsonDocument.Parse(body);
        if (
            tokenDocument.RootElement.ValueKind != JsonValueKind.Object
            || !tokenDocument.RootElement.TryGetProperty("access_token", out var tokenValue)
            || tokenValue.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(tokenValue.GetString())
        )
        {
            await output.WriteLineAsync("OAuth response did not contain an access token.");
            return 1;
        }

        var token = tokenValue.GetString()!;
        var sensitive = new[] { clientId, clientSecret, basic, token };
        await output.WriteLineAsync(
            $"Checked at {DateTimeOffset.UtcNow:O}; region {options.Region}. Status/shape only; no response bodies are saved."
        );
        var failed = false;
        foreach (var target in Targets(options))
        {
            var uri = new Uri(
                $"https://{options.Region}.api.blizzard.com{target.Path}?namespace={Uri.EscapeDataString(target.Namespace)}&locale=en_US"
            );
            using var get = new HttpRequestMessage(HttpMethod.Get, uri);
            get.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var result = await http.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            await output.WriteLineAsync(Safe($"{target.Namespace} {target.Path}: HTTP {(int)result.StatusCode}", sensitive));
            foreach (var header in new[] { "battlenet-namespace", "Cache-Control", "ETag", "Retry-After" })
                if (result.Headers.TryGetValues(header, out var values))
                    await output.WriteLineAsync($"  {header}: {Safe(string.Join(", ", values), sensitive)}");
            if (result.Content.Headers.LastModified is { } modified)
                await output.WriteLineAsync($"  Last-Modified: {modified:O}");

            if (result.IsSuccessStatusCode)
            {
                var data = await ReadBodyAsync(result.Content, cancellationToken);
                await output.WriteLineAsync($"  {(data is null ? "Response exceeded the diagnostic size limit." : Summarize(data))}");
            }
            // 403/404 are observations, not an empty roster or proof a particular route exists.
            // Stop instead of hammering an upstream that asks us to slow down or has failed.
            if (!result.IsSuccessStatusCode && result.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.NotFound))
            {
                failed = true;
                await output.WriteLineAsync("Stopped after authentication, redirect, rate-limit, or upstream failure; no automatic retries.");
                break;
            }
        }

        await output.WriteLineAsync("Account character lists require a separate user-login flow; this app-token probe does not request them.");
        return failed ? 1 : 0;
    }

    private static IEnumerable<(string Path, string Namespace)> Targets(ProbeOptions options)
    {
        var dynamicNamespace = $"dynamic-forever-{options.Region}";
        var profile = $"profile-forever-{options.Region}";
        yield return ("/data/wow/realm/index", $"dynamic-probe-invalid-{options.Region}");
        yield return ("/data/wow/region/index", dynamicNamespace);
        yield return ("/data/wow/realm/index", dynamicNamespace);
        yield return ("/data/wow/connected-realm/index", dynamicNamespace);
        yield return ("/data/wow/playable-class/index", $"static-forever-{options.Region}");
        yield return ("/data/wow/playable-class/index", $"static-{options.StaticBuild}-forever-{options.Region}");
        yield return ("/data/wow/profession/index", $"static-{options.StaticBuild}-forever-{options.Region}");
        if (options.CharacterSlug is { } character)
        {
            var path = $"/profile/wow/character/{Uri.EscapeDataString(character)}";
            yield return (path, profile);
            yield return ($"{path}/professions", profile);
        }
        if (options.GuildSlug is { } guild)
        {
            var prefix = options.RealmSlug is { } realm ? $"{Uri.EscapeDataString(realm)}/" : "";
            var path = $"/data/wow/guild/{prefix}{Uri.EscapeDataString(guild)}";
            yield return (path, profile);
            yield return ($"{path}/roster", profile);
        }
    }

    private static string Summarize(byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return "JSON response is not an object.";
            var facts = new List<string>();
            foreach (var field in new[] { "regions", "realms", "connected_realms", "classes", "professions", "members", "primaries", "secondaries" })
                if (root.TryGetProperty(field, out var array) && array.ValueKind == JsonValueKind.Array)
                    facts.Add($"{field} count={array.GetArrayLength()}");
            // Fixed field-name allowlist only: never print character names, IDs, links, or arbitrary JSON values.
            foreach (var field in new[] { "id", "name", "surname", "level", "playable_class", "guild", "_links" })
                if (root.TryGetProperty(field, out _))
                    facts.Add($"{field} present");
            return facts.Count == 0 ? "JSON object; no recognized summary fields." : string.Join("; ", facts);
        }
        catch (JsonException)
        {
            return "Response was not valid diagnostic JSON; body suppressed.";
        }
    }

    private static string Safe(string value, string[] sensitive)
    {
        foreach (var secret in sensitive.Where(value => !string.IsNullOrEmpty(value)).OrderByDescending(value => value.Length))
            value = value.Replace(secret, "[redacted]", StringComparison.Ordinal);
        // JSON escaping keeps remote header values from injecting terminal control sequences or extra lines.
        return JsonSerializer.Serialize(value.Length > 300 ? value[..300] + "..." : value);
    }

    private static async Task<byte[]?> ReadBodyAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + count > MaximumResponseBytes)
                return null;
            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }
        return buffer.ToArray();
    }
}
