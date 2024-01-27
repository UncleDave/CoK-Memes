namespace ChampionsOfKhazad.Bot;

public class AssistantFollowerResponseStrategy(Assistant assistant, User user, BotContext botContext, string instructions) : IFollowerResponseStrategy
{
    public async Task<string> GetResponseAsync(MessageReceived notification, CancellationToken cancellationToken = default) =>
        await assistant.RespondAsync(
            notification.Message.CleanContent,
            user,
            botContext.Guild.Emotes.Select(x => x.Name),
            await GetContextAsync(notification, cancellationToken),
            instructions: instructions
        );

    private async Task<IEnumerable<Message>> GetContextAsync(MessageReceived notification, CancellationToken cancellationToken) =>
        // Get the unbroken message chain from the same author within the last 60 seconds
        await notification
            .Message.GetPreviousMessagesAsync()
            .TakeWhile(x => x.Author.Id == user.Id && DateTimeOffset.UtcNow - x.Timestamp < TimeSpan.FromSeconds(60))
            .Reverse()
            .Select(x => new Message(x.CleanContent, user))
            .ToListAsync(cancellationToken: cancellationToken);
}
