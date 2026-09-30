using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChampionsOfKhazad.Bot.GenAi;

public record NotebookSource(string Url, ulong AuthorId, string AuthorName, DateTime TimestampUtc, string Content)
{
    public const int MaximumContentLength = 4000;

    // Detect edits even beyond the bounded excerpt retained for review.
    public string ContentHash { get; init; } = string.Empty;

    // Preserve reader-specific redaction/meaning checks without archiving the evidence text.
    public string ViewHash { get; init; } = string.Empty;
    public IReadOnlyList<NotebookMentionedUser> MentionedUsers { get; init; } = [];

    public static string CalculateViewHash(string content, string authorName, IReadOnlyList<NotebookMentionedUser> mentions) =>
        Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(
                        new
                        {
                            content,
                            authorName,
                            mentions = mentions.OrderBy(user => user.Id),
                        }
                    )
                )
            )
        );
}
