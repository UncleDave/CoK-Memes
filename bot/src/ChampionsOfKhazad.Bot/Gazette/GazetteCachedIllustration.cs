using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

public sealed record GazetteCachedIllustration(string EvidenceKey, byte[] Image, DateTimeOffset ExpiresAtUtc)
{
    internal static string CreateEvidenceKey(IReadOnlyList<NotebookSource> sources) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sources.OrderBy(source => source.Url, StringComparer.Ordinal))))
        );
}
