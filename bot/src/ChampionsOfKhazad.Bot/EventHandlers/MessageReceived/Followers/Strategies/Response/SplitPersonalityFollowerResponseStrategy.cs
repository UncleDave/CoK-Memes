using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

public class SplitPersonalityFollowerResponseStrategy(IReadOnlyList<IPersonality> personalities) : IFollowerResponseStrategy
{
    public async Task<string> GetResponseAsync(MessageReceived notification, CancellationToken cancellationToken = default) =>
        await RandomUtils
            .PickRandom(personalities)
            .InvokeAsync(
                new ChatMessage(notification.Message.GetAuthorName(), notification.Message.CleanContent),
                await GetChatHistoryAsync(notification, cancellationToken),
                cancellationToken
            );

    private static ValueTask<List<ChatMessage>> GetChatHistoryAsync(MessageReceived notification, CancellationToken cancellationToken) =>
        notification
            .Message.GetPreviousMessagesAsync()
            .Take(10)
            .Reverse()
            .Select(x => new ChatMessage(x.GetAuthorName(), x.CleanContent))
            .ToListAsync(cancellationToken: cancellationToken);
}
