namespace ChampionsOfKhazad.Bot.GenAi;

public sealed record GazettePublishedEdition(string Id, ulong GuildId, ulong ChannelId, string Text, DateTime ApprovedAtUtc)
{
    public ulong? MessageId { get; init; }
}
