namespace ChampionsOfKhazad.Bot.GenAi;

public record NotebookObservationBatch(ulong ChannelId, ulong LastMessageId, IReadOnlyList<NotebookObservationMessage> Messages);
