using System.Text.RegularExpressions;
using ChampionsOfKhazad.Bot.ChatBot;
using Discord;
using Humanizer;
using Microsoft.Extensions.Options;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace ChampionsOfKhazad.Bot;

// TODO: Threads

public class MentionHandler : IMessageReceivedEventHandler
{
    private static readonly Regex NameExpression =
        new("^[a-zA-Z0-9_-]{1,64}$", RegexOptions.Compiled);

    private readonly MentionHandlerOptions _options;
    private readonly Assistant _assistant;
    private readonly Dictionary<ulong, DateTime> _lastMessageTimeByQuotaUsers = new();
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

    public async Task HandleMessageAsync(IUserMessage message)
    {
        if (
            message.Channel is not ITextChannel textChannel
            || textChannel.Id != _options.ChannelId
            || !message.MentionedUserIds.Contains(_botId)
        )
            return;

        if (_options.HourlyUserQuotas?.TryGetValue(message.Author.Id, out var quota) ?? false)
        {
            if (
                _lastMessageTimeByQuotaUsers.TryGetValue(message.Author.Id, out var lastMessageTime)
            )
            {
                var timeSinceLastMessage = DateTime.UtcNow - lastMessageTime;
                if (timeSinceLastMessage < TimeSpan.FromHours(1))
                {
                    var timeUntilNextMessage = TimeSpan.FromHours(1) - timeSinceLastMessage;
                    await message.ReplyAsync(
                        $"I'm sorry, you have reached your hourly quota of {"message".ToQuantity(quota)}. Please wait {"minutes".ToQuantity(timeUntilNextMessage.TotalMinutes, "0")} before trying again."
                    );
                    return;
                }

                _lastMessageTimeByQuotaUsers[message.Author.Id] = DateTime.UtcNow;
            }
            else
            {
                _lastMessageTimeByQuotaUsers.Add(message.Author.Id, DateTime.UtcNow);
            }
        }

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

    public override string ToString() => $"{nameof(MentionHandler)}";
}
