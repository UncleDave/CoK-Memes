namespace ChampionsOfKhazad.Bot.GenAi;

public record NotebookChannelAttempt(ulong ChannelId, DateTime AttemptedAtUtc)
{
    public DateTime? FirstPriorityAtUtc { get; init; }
}
