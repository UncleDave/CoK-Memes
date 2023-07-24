using ChampionsOfKhazad.Bot.ChatBot;
using Discord;
using Microsoft.Extensions.Options;
using OpenAI.ObjectModels.RequestModels;

namespace ChampionsOfKhazad.Bot;

public class SycophantHandler : IMessageReceivedEventHandler
{
    private readonly SycophantHandlerOptions _options;
    private readonly Assistant _assistant;
    private readonly BotContext _context;

    public SycophantHandler(
        IOptions<SycophantHandlerOptions> options,
        Assistant assistant,
        BotContext context
    )
    {
        _options = options.Value;
        _assistant = assistant;
        _context = context;
    }

    public Task HandleMessageAsync(IUserMessage message)
    {
        if (
            message.Channel is not ITextChannel textChannel
            || message.Author.Id != _options.UserId
            || message.MentionedUserIds.Contains(_context.BotId)
            || !RandomUtils.Roll(1)
        )
            return Task.CompletedTask;

#pragma warning disable CS4014
        Task.Run(async () =>
#pragma warning restore CS4014
        {
            using var typing = textChannel.EnterTypingState();

            var user = new User { Id = message.Author.Id, Name = _options.UserName };

            // Get the unbroken message chain from the same author within the last 30 seconds
            var recentUserMessages = await message
                .GetPreviousMessagesAsync()
                .TakeWhile(
                    x =>
                        x.Author.Id == _options.UserId
                        && DateTimeOffset.UtcNow - x.Timestamp < TimeSpan.FromSeconds(30)
                )
                .Reverse()
                .Select(x => ChatMessage.FromUser(x.CleanContent, user.Name))
                .ToListAsync();

            var response = await _assistant.RespondAsync(
                message.CleanContent,
                user,
                recentUserMessages,
                instructions: "You are a sycophant. You will agree with and echo everything the users says but will not add anything of value. You will try to suck up to them as much as possible. You are not too bright."
            );

            await textChannel.SendMessageAsync(response);
        });

        return Task.CompletedTask;
    }
}
