using System.Text.RegularExpressions;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using MediatR;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class MentionHandler(IOptions<MentionHandlerOptions> options, BotContext context, ILorekeeperPersonality lorekeeper)
    : INotificationHandler<MessageReceived>
{
    private static readonly Regex EmojiExpression = new(@":(?<name>\w+):", RegexOptions.Compiled);
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

        var previousMessages = await message
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
            previousMessages,
            context.Guild.Emotes.Select(x => x.Name),
            cancellationToken
        );

        await message.ReplyAsync(ProcessEmojis(response));
    }

    private string GetMessageCleanContentWithoutBotMention(IMessage message)
    {
        var botTag = $"{context.Client.CurrentUser.Username}#{context.Client.CurrentUser.Discriminator}";
        var botTagIndex = message.CleanContent.IndexOf(botTag, StringComparison.Ordinal);
        var messageContentWithoutBotMentionAtStart = botTagIndex == 1 ? message.CleanContent[(botTag.Length + 1)..].Trim() : message.CleanContent;

        return messageContentWithoutBotMentionAtStart.Replace(botTag, GenAi.Constants.LorekeeperName);
    }

    private string ProcessEmojis(string message)
    {
        var matches = EmojiExpression.Matches(message);

        foreach (Match match in matches)
        {
            var emojiName = match.Groups["name"].Value;

            if (context.Guild.Emotes.FirstOrDefault(x => string.Equals(x.Name, emojiName, StringComparison.InvariantCultureIgnoreCase)) is { } emoji)
                message = message.Replace(match.Value, emoji.ToString());
        }

        return message;
    }

    public override string ToString() => nameof(MentionHandler);
}
