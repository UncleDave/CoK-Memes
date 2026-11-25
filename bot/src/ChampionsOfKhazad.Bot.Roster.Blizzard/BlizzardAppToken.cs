using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ChampionsOfKhazad.Bot.Roster.Blizzard;

internal sealed class BlizzardAppToken(string value, DateTimeOffset renewAtUtc)
{
    public string Value { get; } = value;
    public DateTimeOffset RenewAtUtc { get; } = renewAtUtc;

    public static HttpRequestMessage CreateRequest(BlizzardRosterOptions options) =>
        new(HttpMethod.Post, "https://oauth.battle.net/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}"))
                ),
            },
        };

    public static BlizzardAppToken Parse(JsonElement root, DateTimeOffset now)
    {
        if (
            root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("access_token", out var accessToken)
            || accessToken.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(accessToken.GetString())
            || !root.TryGetProperty("expires_in", out var lifetime)
            || lifetime.ValueKind != JsonValueKind.Number
            || !lifetime.TryGetInt32(out var seconds)
            || seconds <= 0
        )
            throw new BlizzardApiException("Blizzard OAuth response did not contain a usable token and expiry.");
        return new(accessToken.GetString()!, now.AddSeconds(Math.Max(1, seconds - Math.Min(60, seconds / 10))));
    }
}
