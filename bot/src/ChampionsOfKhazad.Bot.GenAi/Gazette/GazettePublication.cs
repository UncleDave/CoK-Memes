namespace ChampionsOfKhazad.Bot.GenAi;

public sealed record GazettePublication(long Number, string Token, ulong ChannelId, DateTime AttemptedAtUtc, ulong? MessageId = null);
