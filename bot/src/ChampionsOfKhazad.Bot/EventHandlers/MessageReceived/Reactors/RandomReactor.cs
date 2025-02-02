using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot;

public class RandomReactor(ILogger<RandomReactor> logger, ICompletionService completionService, BotContext botContext)
    : INotificationHandler<MessageReceived>
{
    public async Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        var message = notification.Message;

        if (message.Channel is not ITextChannel || string.IsNullOrWhiteSpace(message.CleanContent))
            return;

        var (success, roll) = RandomUtils.Roll(1);

        logger.LogInformation(
            "{ReactorType} rolling for message from {Author}: {Roll} - {Result}",
            GetType().Name,
            message.Author.GlobalName ?? message.Author.Username,
            roll,
            success ? "Success" : "Failure"
        );

        if (!success)
            return;

        var guildEmojis = botContext.Guild.Emotes;

        var chatHistory = new ChatHistory(
            string.Join(
                '\n',
                "Choose the most appropriate emoji reaction to the user's message. You may choose any custom emoji from the following list, or any standard unicode emoji.\n",
                string.Join(' ', guildEmojis.Select(x => x.Name)),
                "\nRespond with the chosen emoji and nothing else."
            )
        )
        {
            new ChatMessageContent(AuthorRole.User, message.CleanContent) { AuthorName = message.GetOpenAiFriendlyAuthorName() },
        };

        var completionResult = await completionService.InvokeAsync(chatHistory, cancellationToken);

        IEmote? chosenGuildEmoji = guildEmojis.SingleOrDefault(x =>
            string.Equals(x.Name, completionResult, StringComparison.InvariantCultureIgnoreCase)
        );

        var chosenEmoji = chosenGuildEmoji ?? new Emoji(completionResult);

        await message.AddReactionAsync(chosenEmoji);
    }

    public override string ToString() => nameof(RandomReactor);
}
