using System.Text.RegularExpressions;
using Discord;
using MediatR;
using Microsoft.Extensions.Options;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace ChampionsOfKhazad.Bot;

public class MentionHandler(IOptions<MentionHandlerOptions> options, Assistant assistant, BotContext context) : INotificationHandler<MessageReceived>
{
    private static readonly Regex NameExpression = new("^[a-zA-Z0-9_-]{1,64}$", RegexOptions.Compiled);

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

        var user = new User { Id = message.Author.Id, Name = GetFriendlyAuthorName(message) };

        var previousMessages = await message
            .GetPreviousMessagesAsync()
            .Take(20)
            .Reverse()
            .Select(x => new ChatMessage(GetMessageRole(x), x.CleanContent, GetFriendlyAuthorName(x)))
            .ToListAsync(cancellationToken: cancellationToken);

        if (message.Author.Id == _options.CringeAsideUserId)
            previousMessages.Add(ChatMessage.FromSystem("Include cringe aside somewhere in your response."));

        var response = await assistant.RespondAsync(
            message.CleanContent,
            user,
            context.Guild.Emotes.Select(x => x.Name),
            previousMessages,
            message.ReferencedMessage is not null
                ? new ChatMessage(
                    GetMessageRole(message.ReferencedMessage),
                    message.ReferencedMessage.CleanContent,
                    GetFriendlyAuthorName(message.ReferencedMessage)
                )
                : null
        );

        await message.ReplyAsync(response);
    }

    private static string GetFriendlyAuthorName(IMessage message) =>
        message.Author is IGuildUser { DisplayName: not null } guildUser && NameExpression.IsMatch(guildUser.DisplayName)
            ? guildUser.DisplayName
            : message.Author.GlobalName is not null && NameExpression.IsMatch(message.Author.GlobalName)
                ? message.Author.GlobalName
                : message.Author.Username is not null && NameExpression.IsMatch(message.Author.Username)
                    ? message.Author.Username
                    : message.Author.Id.ToString();

    private string GetMessageRole(IMessage message) =>
        message.Author.Id == context.BotId ? StaticValues.ChatMessageRoles.Assistant : StaticValues.ChatMessageRoles.User;

    public override string ToString() => nameof(MentionHandler);
}
