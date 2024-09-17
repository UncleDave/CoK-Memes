using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

public class SplitPersonalityFollowerResponseStrategy(IReadOnlyList<IPersonality> personalities, ulong botId) : IFollowerResponseStrategy
{
    public async Task<string> GetResponseAsync(MessageReceived notification, CancellationToken cancellationToken = default) =>
        await RandomUtils
            .PickRandom(personalities)
            .InvokeAsync(await notification.Message.GetChatHistoryAsync(10, botId, "You", cancellationToken), cancellationToken);
}
