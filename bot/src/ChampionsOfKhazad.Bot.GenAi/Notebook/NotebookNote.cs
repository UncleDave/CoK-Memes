namespace ChampionsOfKhazad.Bot.GenAi;

public record NotebookNote(
    string Id,
    ulong RequestedBy,
    string Subject,
    string Kind,
    string Content,
    string Reason,
    IReadOnlyList<NotebookSource> Sources,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc
)
{
    public NotebookOrigin Origin { get; init; }
    public bool ReviewDelivered { get; init; }
    public string ReviewReason { get; init; } = string.Empty;
    public DateTime? DiscardedAtUtc { get; init; }

    public bool IsActive(DateTime now) => ReviewDelivered && DiscardedAtUtc is null && ExpiresAtUtc > now;
}
