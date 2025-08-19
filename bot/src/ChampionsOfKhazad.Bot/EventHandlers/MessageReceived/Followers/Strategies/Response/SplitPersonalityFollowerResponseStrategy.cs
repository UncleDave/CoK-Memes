using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

public class SplitPersonalityFollowerResponseStrategy(
    IReadOnlyList<IPersonality> personalities,
    ulong botId,
    IDictionary<string, object?>? arguments = null
) : IFollowerResponseStrategy
{
    private static readonly IDictionary<string, object?> DefaultArguments = new Dictionary<string, object?>();

    public async Task<string> GetResponseAsync(MessageReceived notification, CancellationToken cancellationToken = default) =>
        await RandomUtils
            .PickRandom(personalities)
            .InvokeAsync(
                await notification.Message.GetChatHistoryAsync(10, botId, "You", cancellationToken),
                arguments ?? DefaultArguments,
                cancellationToken
            );
}
