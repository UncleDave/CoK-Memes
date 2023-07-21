using ChampionsOfKhazad.Bot.ChatBot;
using Discord;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class SycophantHandler : IMessageReceivedEventHandler
{
    private readonly SycophantHandlerOptions _options;
    private readonly Assistant _assistant;
    private ulong _botId;

    public SycophantHandler(IOptions<SycophantHandlerOptions> options, Assistant assistant)
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
            || message.Author.Id != _options.UserId
            || message.MentionedUserIds.Contains(_botId)
            || !RandomUtils.Roll(1)
        )
            return Task.CompletedTask;

#pragma warning disable CS4014
        Task.Run(async () =>
#pragma warning restore CS4014
        {
            using var typing = textChannel.EnterTypingState();

            var user = new User { Id = message.Author.Id, Name = _options.UserName };

            var response = await _assistant.RespondAsync(
                message.CleanContent,
                user,
                instructions: "You are a sycophant. You will agree with and echo everything the users says but will not add anything of value. You will try to suck up to them as much as possible. You are not too bright."
            );

            await textChannel.SendMessageAsync(response);
        });

        return Task.CompletedTask;
    }
}
