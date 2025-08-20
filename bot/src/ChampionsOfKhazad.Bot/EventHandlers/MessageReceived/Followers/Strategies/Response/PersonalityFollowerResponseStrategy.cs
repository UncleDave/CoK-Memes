using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

public class PersonalityFollowerResponseStrategy(IPersonality personality, ulong botId, IDictionary<string, object?>? arguments = null)
    : IFollowerResponseStrategy
{
    private static readonly IDictionary<string, object?> DefaultArguments = new Dictionary<string, object?>();

    public async Task<string> GetResponseAsync(MessageReceived notification, CancellationToken cancellationToken = default) =>
        await personality.InvokeAsync(
            await notification.Message.GetChatHistoryAsync(10, botId, "You", cancellationToken),
            notification.Message.ToMessageContext(),
            arguments ?? DefaultArguments,
            cancellationToken
        );
}
