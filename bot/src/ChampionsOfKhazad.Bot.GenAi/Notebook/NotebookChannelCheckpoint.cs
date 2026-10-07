namespace ChampionsOfKhazad.Bot.GenAi;

public record NotebookChannelCheckpoint(ulong ChannelId, ulong LastMessageId, DateTime ScannedAtUtc);
