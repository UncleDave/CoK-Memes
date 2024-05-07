namespace ChampionsOfKhazad.Bot;

public class SplitPersonalityAssistantFollowerResponseStrategy(
    Assistant assistant,
    User user,
    BotContext botContext,
    IReadOnlyList<string> instructions
) : IFollowerResponseStrategy
{
    public async Task<string> GetResponseAsync(MessageReceived notification, CancellationToken cancellationToken = default)
    {
        var chosenInstructions = RandomUtils.PickRandom(instructions);

        return await assistant.RespondAsync(
            notification.Message.CleanContent,
            user,
            botContext.Guild.Emotes.Select(x => x.Name),
            await GetContextAsync(notification, cancellationToken),
            instructions: chosenInstructions
        );
    }

    private static async Task<IEnumerable<Message>> GetContextAsync(MessageReceived notification, CancellationToken cancellationToken) =>
        // Get messages within the last 60 seconds
        await notification
            .Message.GetPreviousMessagesAsync()
            .TakeWhile(x => DateTimeOffset.UtcNow - x.Timestamp < TimeSpan.FromSeconds(60))
            .Reverse()
            .Select(x => new Message(x.CleanContent, new User(x.Author.Id, x.GetAuthorName())))
            .ToListAsync(cancellationToken: cancellationToken);
}
