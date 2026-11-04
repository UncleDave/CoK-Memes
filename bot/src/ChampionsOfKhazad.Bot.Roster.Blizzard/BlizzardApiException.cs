using System.Net;

namespace ChampionsOfKhazad.Bot.Roster.Blizzard;

// Do not attach response bodies, tokens, credentials, or transport exceptions to logged failures.
public sealed class BlizzardApiException(string message, HttpStatusCode? statusCode = null, string? resource = null) : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
    public string? Resource { get; } = resource;
}
