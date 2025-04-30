using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

public class PersonalityFollowerResponseStrategy(IPersonality personality, ulong botId) : IFollowerResponseStrategy
{
    public async Task<string> GetResponseAsync(MessageReceived notification, CancellationToken cancellationToken = default) =>
        await personality.InvokeAsync(
            await notification.Message.GetChatHistoryAsync(10, botId, "You", cancellationToken),
            notification.Message.Author.Id,
            cancellationToken
        );
}
