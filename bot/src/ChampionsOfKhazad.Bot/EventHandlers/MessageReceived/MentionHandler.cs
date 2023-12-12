using Discord;
using MediatR;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class MentionHandler(IOptions<MentionHandlerOptions> options, Assistant assistant, BotContext context) : INotificationHandler<MessageReceived>
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

        var user = new User(message.Author.Id, message.GetFriendlyAuthorName());

        var previousMessages = await message
            .GetPreviousMessagesAsync()
            .Take(20)
            .Reverse()
            .Select(x => new Message(x.CleanContent, new User(x.Author.Id, x.GetFriendlyAuthorName()), GetMessageRole(x)))
            .ToListAsync(cancellationToken: cancellationToken);

        if (message.Author.Id == _options.CringeAsideUserId)
            previousMessages.Add(new Message("Include cringe aside somewhere in your response.", null, Role.System));

        var response = await assistant.RespondAsync(
            message.CleanContent,
            user,
            context.Guild.Emotes.Select(x => x.Name),
            previousMessages,
            message.ReferencedMessage is not null
                ? new Message(
                    message.ReferencedMessage.CleanContent,
                    new User(message.ReferencedMessage.Author.Id, message.ReferencedMessage.GetFriendlyAuthorName()),
                    GetMessageRole(message.ReferencedMessage)
                )
                : null
        );

        await message.ReplyAsync(response);
    }

    private Role GetMessageRole(IMessage message) => message.Author.Id == context.BotId ? Role.Assistant : Role.User;

    public override string ToString() => nameof(MentionHandler);
}
