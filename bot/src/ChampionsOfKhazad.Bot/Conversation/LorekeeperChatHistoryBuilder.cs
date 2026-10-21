using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class LorekeeperChatHistoryBuilder(
    SharedDiscordRestClient restClient,
    IOptions<DirectMessageHandlerOptions> adminOptions,
    ILogger<LorekeeperChatHistoryBuilder> logger
)
{
    private const int RecentMessageLimit = 20;
    private const int ReplyRecentMessageLimit = 8;
    private const int ReplyChainLimit = 8;
    private const int MaximumScannedMessages = 200;

    public async Task<ChatHistory> BuildAsync(IUserMessage trigger, ulong botId, CancellationToken cancellationToken = default)
    {
        var requestOptions = new RequestOptions { CancelToken = cancellationToken };
        // The socket reader can stitch a discontinuous cache to older downloads, skipping markers.
        // Borrow DiscordSocketClient.Rest without making DI own/dispose it; pagination must bypass the socket cache.
        var historyChannel = await restClient.Client.GetChannelAsync(trigger.Channel.Id, options: requestOptions) as IMessageChannel;
        if (historyChannel is null || historyChannel.Id != trigger.Channel.Id)
            throw new InvalidOperationException("The invoking Discord channel is unavailable for conversation history.");
        Dictionary<ulong, IMessage> scanned = [];
        ulong? boundary = null;
        var beforeId = trigger.Id;
        var scanCount = 0;
        var reachedStart = false;
        var scanFailed = false;

        await ReadMoreAsync();
        var recent = scanned.Values.OrderByDescending(message => message.Id).ToArray();
        Dictionary<ulong, IMessage> selected = [];
        Dictionary<ulong, string> kinds = [];
        Dictionary<ulong, string> replyNotes = [];
        HashSet<ulong> visited = [trigger.Id];
        var current = trigger;

        for (var depth = 0; depth < ReplyChainLimit; depth++)
        {
            var reference = current.Reference;
            if (reference is null || !reference.MessageId.IsSpecified || reference.ReferenceType.GetValueOrDefault() != MessageReferenceType.Default)
                break;

            var targetId = reference.MessageId.Value;
            if (reference.ChannelId != trigger.Channel.Id || targetId >= current.Id || !visited.Add(targetId))
            {
                replyNotes[current.Id] = "Reply target is unavailable in this channel.";
                break;
            }

            // Check the intervening channel history before using even an embedded reply target.
            // Otherwise an old reply could skip a reset marker outside the recent-message window.
            while (boundary is null && !reachedStart && !scanFailed && beforeId > targetId && scanCount < MaximumScannedMessages)
            {
                try
                {
                    await ReadMoreAsync();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    scanFailed = true;
                    logger.LogWarning(
                        exception,
                        "Could not check reply context before message {MessageId} in channel {ChannelId}",
                        targetId,
                        trigger.Channel.Id
                    );
                }
            }

            if (boundary is not null && targetId <= boundary)
            {
                replyNotes[current.Id] =
                    "Reply context was omitted at the conversation reset boundary; do not use or retrieve that closed conversation.";
                break;
            }
            if (scanFailed || (!reachedStart && beforeId > targetId))
            {
                replyNotes[current.Id] = "Reply context is unavailable: the intervening history could not be checked within the lookup limit.";
                break;
            }

            IMessage? target = scanned.GetValueOrDefault(targetId);
            target ??= current.ReferencedMessage;
            if (target is null)
            {
                try
                {
                    target = await historyChannel.GetMessageAsync(targetId, options: requestOptions);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Could not read reply target {MessageId} in channel {ChannelId}", targetId, trigger.Channel.Id);
                }
            }

            if (target is null || target.Id != targetId || target.Channel.Id != trigger.Channel.Id)
            {
                replyNotes[current.Id] = "Reply target is unavailable; do not guess its contents.";
                break;
            }
            if (LorekeeperContextMarker.IsMatch(target, botId, adminOptions.Value.AdminUserId))
            {
                replyNotes[current.Id] = "The reply target is a conversation reset marker, not a question.";
                break;
            }

            selected[target.Id] = target;
            kinds[target.Id] = depth == 0 ? "replyTarget" : "replyAncestor";
            if (target is not IUserMessage userMessage)
                break;

            current = userMessage;
            if (depth == ReplyChainLimit - 1 && current.Reference?.MessageId.IsSpecified == true)
                replyNotes[current.Id] = "Earlier reply context was omitted at the reply-chain limit.";
        }

        var recentLimit = trigger.Reference?.MessageId.IsSpecified == true ? ReplyRecentMessageLimit : RecentMessageLimit;
        var added = 0;
        foreach (var message in recent)
        {
            if (boundary is not null && message.Id <= boundary)
                break;
            if (selected.ContainsKey(message.Id) || !IsUsefulContext(message, botId))
                continue;
            if (added >= recentLimit)
                break;

            selected[message.Id] = message;
            kinds[message.Id] = "recent";
            added++;
        }

        var history = new ChatHistory(
            selected
                .Values.OrderBy(message => message.Id)
                .Select(message =>
                    DiscordConversationMessage
                        .FromDiscord(message, botId, kinds[message.Id], replyNotes.GetValueOrDefault(message.Id))
                        .ToChatMessage(GenAi.Constants.OpenAiFriendlyLorekeeperName)
                )
        );
        history.Add(
            DiscordConversationMessage
                .FromDiscord(trigger, botId, "currentRequest", replyNotes.GetValueOrDefault(trigger.Id))
                .ToChatMessage(GenAi.Constants.OpenAiFriendlyLorekeeperName)
        );
        return history;

        async Task ReadMoreAsync()
        {
            var pageSize = scanCount == 0 ? RecentMessageLimit : DiscordConfig.MaxMessagesPerBatch;
            var limit = Math.Min(pageSize, MaximumScannedMessages - scanCount);
            var batch = (await historyChannel.GetMessagesAsync(beforeId, Direction.Before, limit, options: requestOptions).FlattenAsync())
                .OrderByDescending(message => message.Id)
                .ToArray();
            scanCount += batch.Length;
            var previousBeforeId = beforeId;
            foreach (var message in batch)
            {
                if (message.Channel.Id != trigger.Channel.Id || message.Id >= previousBeforeId)
                    continue;
                beforeId = Math.Min(beforeId, message.Id);
                if (LorekeeperContextMarker.IsMatch(message, botId, adminOptions.Value.AdminUserId))
                {
                    boundary = message.Id;
                    break;
                }
                scanned[message.Id] = message;
            }
            reachedStart = batch.Length < limit;
            if (batch.Length > 0 && beforeId == previousBeforeId)
                scanFailed = true;
        }
    }

    private static bool IsUsefulContext(IMessage message, ulong botId) =>
        !(message.Author.Id == botId && message.CleanContent.StartsWith(GenAi.Constants.ImageGenerationConfirmationMessage, StringComparison.Ordinal))
        && (
            !string.IsNullOrWhiteSpace(message.CleanContent)
            || message.Attachments.Any(attachment => MessageExtensions.GetImageMediaType(attachment.Filename) is not null)
        );
}
