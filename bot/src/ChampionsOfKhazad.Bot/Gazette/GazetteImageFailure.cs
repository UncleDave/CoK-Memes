using System.ClientModel;
using System.Text.Json;

namespace ChampionsOfKhazad.Bot;

internal sealed record GazetteImageFailure(int? HttpStatus, string Code, string Parameter, bool TimedOut)
{
    private static readonly HashSet<string> KnownCodes = new(StringComparer.Ordinal)
    {
        "invalid_request_error",
        "invalid_value",
        "unsupported_parameter",
        "unsupported_value",
        "invalid_api_key",
        "authentication_error",
        "permission_error",
        "model_not_found",
        "model_access_denied",
        "insufficient_quota",
        "rate_limit_error",
        "rate_limit_exceeded",
        "content_policy_violation",
        "moderation_blocked",
        "server_error",
        "image_generation_user_error",
        "image_generation_error",
        "image_generation_timeout",
    };
    private static readonly HashSet<string> KnownParameters = new(StringComparer.Ordinal)
    {
        "model",
        "quality",
        "size",
        "response_format",
        "output_format",
        "background",
        "moderation",
        "n",
        "prompt",
    };

    public string Description =>
        TimedOut ? "the image request timed out"
        : HttpStatus is > 0 ? $"the image API returned HTTP {HttpStatus} (code {Code}; parameter {Parameter})"
        : HttpStatus == 0 ? "the image client received no HTTP response"
        : "image generation failed";

    public static GazetteImageFailure FromException(Exception exception)
    {
        if (exception is OperationCanceledException)
            return new(null, "Timeout", "none", true);
        if (exception is not ClientResultException failure)
            return new(null, "InternalFailure", "none", false);
        try
        {
            var content = failure.GetRawResponse()?.Content;
            return FromPayload(failure.Status, content is not null && content.ToMemory().Length <= 16384 ? content.ToString() : null);
        }
        catch
        {
            // A disposed/unbuffered provider response must not mask the original error or expose its raw message.
            return new(failure.Status, "unspecified", "none", false);
        }
    }

    internal static GazetteImageFailure FromPayload(int status, string? payload)
    {
        var code = "unspecified";
        var parameter = "none";
        if (payload is { Length: <= 16384 })
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
                {
                    var candidate = ReadString(error, "code");
                    if (candidate is null || !KnownCodes.Contains(candidate))
                        candidate = ReadString(error, "type");
                    if (candidate is not null && KnownCodes.Contains(candidate))
                        code = candidate;
                    candidate = ReadString(error, "param");
                    if (candidate is not null && KnownParameters.Contains(candidate))
                        parameter = candidate;
                }
            }
            catch (JsonException) { }
        }
        return new(status, code, parameter, false);
    }

    private static string? ReadString(JsonElement error, string property) =>
        error.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
