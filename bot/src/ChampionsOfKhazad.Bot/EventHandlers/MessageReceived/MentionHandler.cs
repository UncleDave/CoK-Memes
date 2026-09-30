using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using MediatR;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class MentionHandler(
    IOptions<MentionHandlerOptions> options,
    BotContext context,
    ICompletionService completionService,
    IOptions<DirectMessageHandlerOptions> adminOptions,
    LorekeeperChatHistoryBuilder historyBuilder
) : INotificationHandler<MessageReceived>
{
    private readonly MentionHandlerOptions _options = options.Value;

    public async Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        var message = notification.Message;

        if (message.Author.IsBot)
            return;

        if (
            message.Channel is not ITextChannel textChannel
            || _options.ChannelIds.All(x => x != textChannel.CategoryId && x != textChannel.Id)
            || !message.MentionedUserIds.Contains(context.BotId)
        )
            return;

        if (LorekeeperContextMarker.IsMatch(message, context.BotId, adminOptions.Value.AdminUserId))
        {
            await message.AddReactionAsync(new Emoji("🧠"), new RequestOptions { CancelToken = cancellationToken });
            return;
        }

        using var typing = textChannel.EnterTypingState();

        var chatHistory = await historyBuilder.BuildAsync(message, context.BotId, cancellationToken);
        var response = await completionService.Lorekeeper.InvokeAsync(chatHistory, notification.Message.ToMessageContext(), cancellationToken);

        await message.ReplyInChunksAsync(response, cancellationToken);
    }

    public override string ToString() => nameof(MentionHandler);
}
