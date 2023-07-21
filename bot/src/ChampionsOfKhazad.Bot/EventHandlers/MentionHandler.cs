using System.Text.RegularExpressions;
using ChampionsOfKhazad.Bot.ChatBot;
using Discord;
using Microsoft.Extensions.Options;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace ChampionsOfKhazad.Bot;

public class MentionHandler : IMessageReceivedEventHandler
{
    private static readonly Regex NameExpression =
        new("^[a-zA-Z0-9_-]{1,64}$", RegexOptions.Compiled);

    private readonly MentionHandlerOptions _options;
    private readonly Assistant _assistant;
    private ulong _botId;

    public MentionHandler(IOptions<MentionHandlerOptions> options, Assistant assistant)
    {
        _options = options.Value;
        _assistant = assistant;
    }

    public Task StartAsync(BotContext context)
    {
        _botId = context.BotId;
        return Task.CompletedTask;
    }

    public Task HandleMessageAsync(IUserMessage message)
    {
        if (
            message.Channel is not ITextChannel textChannel
            || !message.MentionedUserIds.Contains(_botId)
        )
            return Task.CompletedTask;

#pragma warning disable CS4014
        Task.Run(async () =>
#pragma warning restore CS4014
        {
            using var typing = textChannel.EnterTypingState();

            var user = new User { Id = message.Author.Id, Name = GetFriendlyAuthorName(message) };

            var previousMessages = await message
                .GetPreviousMessagesAsync()
                .Take(20)
                .Reverse()
                .Select(
                    x =>
                        new ChatMessage(GetMessageRole(x), x.CleanContent, GetFriendlyAuthorName(x))
                )
                .ToListAsync();

            if (message.Author.Id == _options.CringeAsideUserId)
                previousMessages.Add(
                    ChatMessage.FromSystem("Include cringe aside somewhere in your response.")
                );

            var response = await _assistant.RespondAsync(
                message.CleanContent,
                user,
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
        });

        return Task.CompletedTask;
    }

    private static string GetFriendlyAuthorName(IMessage message) =>
        message.Author is IGuildUser { DisplayName: not null } guildUser
        && NameExpression.IsMatch(guildUser.DisplayName)
            ? guildUser.DisplayName
            : message.Author.GlobalName is not null
            && NameExpression.IsMatch(message.Author.GlobalName)
                ? message.Author.GlobalName
                : message.Author.Username is not null
                && NameExpression.IsMatch(message.Author.Username)
                    ? message.Author.Username
                    : message.Author.Id.ToString();

    private string GetMessageRole(IMessage message) =>
        message.Author.Id == _botId
            ? StaticValues.ChatMessageRoles.Assistant
            : StaticValues.ChatMessageRoles.User;

    public override string ToString() => nameof(MentionHandler);
}
