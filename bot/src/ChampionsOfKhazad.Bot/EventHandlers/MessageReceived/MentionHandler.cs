using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using MediatR;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class MentionHandler(IOptions<MentionHandlerOptions> options, BotContext context, ICompletionService completionService, IMessageContext messageContext)
    : INotificationHandler<MessageReceived>
{
    private readonly MentionHandlerOptions _options = options.Value;

    public async Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        var message = notification.Message;

        if (
            message.Channel is not ITextChannel textChannel
            || _options.ChannelIds.All(x => x != textChannel.CategoryId && x != textChannel.Id)
            || !message.MentionedUserIds.Contains(context.BotId)
        )
            return;

        using var typing = textChannel.EnterTypingState();

        var chatHistory = await message.GetChatHistoryAsync(20, context.BotId, GenAi.Constants.OpenAiFriendlyLorekeeperName, cancellationToken);
        var response = await completionService.Lorekeeper.InvokeAsync(chatHistory, cancellationToken);

        await message.ReplyAsync(response);
    }

    public override string ToString() => nameof(MentionHandler);
}
