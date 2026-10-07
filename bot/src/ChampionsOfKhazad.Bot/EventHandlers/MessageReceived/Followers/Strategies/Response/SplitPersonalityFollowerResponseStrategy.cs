using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.AI;

namespace ChampionsOfKhazad.Bot;

public class SplitPersonalityFollowerResponseStrategy(IReadOnlyList<IPersonality> personalities, ulong botId, string? invocationContext = null)
    : IFollowerResponseStrategy
{
    public async Task<string> GetResponseAsync(MessageReceived notification, CancellationToken cancellationToken = default)
    {
        var history = await notification.Message.GetChatHistoryAsync(10, botId, "You", cancellationToken);

        if (invocationContext is not null)
            history.Insert(0, new ChatMessage(ChatRole.System, invocationContext));

        return await RandomUtils.PickRandom(personalities).InvokeAsync(history, notification.Message.ToMessageContext(), cancellationToken);
    }
}
