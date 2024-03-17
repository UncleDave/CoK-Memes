using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using MediatR;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class MentionHandler(IOptions<MentionHandlerOptions> options, BotContext context, ILorekeeperPersonality lorekeeper)
    : INotificationHandler<MessageReceived>
{
    private readonly MentionHandlerOptions _options = options.Value;

    public async Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        var message = notification.Message;

        if (
            message.Channel is not ITextChannel textChannel
            || (textChannel.CategoryId != _options.ChannelId && textChannel.Id != _options.ChannelId)
            || !message.MentionedUserIds.Contains(context.BotId)
        )
            return;

        using var typing = textChannel.EnterTypingState();

        var previousMessages = message
            .GetPreviousMessagesAsync()
            .Take(20)
            .Reverse()
            .Select(x => new ChatMessage(
                x.Author.Id == context.BotId ? GenAi.Constants.LorekeeperName : x.GetAuthorName(),
                GetMessageCleanContentWithoutBotMention(x)
            ))
            .ToListAsync(cancellationToken);

        var response = await lorekeeper.InvokeAsync(
            new ChatMessage(message.GetAuthorName(), GetMessageCleanContentWithoutBotMention(message)),
            await previousMessages,
            cancellationToken
        );

        await message.ReplyAsync(response);
    }

    private string GetMessageCleanContentWithoutBotMention(IMessage message)
    {
        var botTag = $"{context.Client.CurrentUser.Username}#{context.Client.CurrentUser.Discriminator}";
        var botTagIndex = message.CleanContent.IndexOf(botTag, StringComparison.Ordinal);
        var messageContentWithoutBotMentionAtStart = botTagIndex == 1 ? message.CleanContent[(botTag.Length + 1)..].Trim() : message.CleanContent;

        return messageContentWithoutBotMentionAtStart.Replace(botTag, GenAi.Constants.LorekeeperName);
    }

    public override string ToString() => nameof(MentionHandler);
}
