using System.Net;
using System.Security.Cryptography;
using System.Text;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Discord.Net;

namespace ChampionsOfKhazad.Bot;

internal sealed class DiscordNotebookSourceReader(
    SharedDiscordRestClient restClient,
    ulong guildId,
    Func<IReadOnlyDictionary<ulong, string>> getAccessibleChannels,
    Func<ulong, string?> getUserName
)
{
    private const int MaximumBatchSize = 100;

    public IReadOnlyList<ulong> GetChannelIds() => getAccessibleChannels().Keys.Order().ToArray();

    internal async Task<DiscordHistoryPage?> ReadHistoryPageAsync(
        ulong channelId,
        DateTimeOffset since,
        DateTimeOffset until,
        ulong? before,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!getAccessibleChannels().ContainsKey(channelId))
            return null;
        var request = new RequestOptions { CancelToken = cancellationToken };
        var channel = await GetChannelAsync(channelId, request);
        if (channel is null)
            return null;
        var cursor = before ?? SnowflakeUtils.ToSnowflake(until.AddMilliseconds(1));
        var messages = (await channel.GetMessagesAsync(cursor, Direction.Before, MaximumBatchSize, options: request).FlattenAsync())
            .Where(message => message.Channel.Id == channelId && message.Id < cursor)
            .DistinctBy(message => message.Id)
            .OrderBy(message => message.Id)
            .ToArray();
        var channels = getAccessibleChannels();
        if (!channels.ContainsKey(channelId))
            return null;
        var inWindow = messages.Where(message => message.Timestamp >= since && message.Timestamp <= until).ToArray();
        var sources = inWindow.Select(message => CreateSource(message, channelId, message.Id, channels)).OfType<NotebookSource>().ToArray();
        if (!getAccessibleChannels().ContainsKey(channelId))
            return null;
        var oldest = messages.FirstOrDefault();
        return new(
            sources,
            oldest?.Id,
            messages.Length < MaximumBatchSize || oldest?.Timestamp < since,
            messages.Length,
            inWindow.Length - sources.Length
        );
    }

    public async Task<NotebookObservationBatch?> ReadBatchAsync(ulong channelId, ulong afterMessageId, int limit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!getAccessibleChannels().ContainsKey(channelId))
            return null;

        IMessage[] messages;
        try
        {
            var requestOptions = new RequestOptions { CancelToken = cancellationToken };
            var channel = await GetChannelAsync(channelId, requestOptions);
            if (channel is null)
                return null;
            var size = Math.Clamp(limit, 1, MaximumBatchSize);
            var batches = channel.GetMessagesAsync(afterMessageId, Direction.After, size, options: requestOptions);
            messages = (await batches.FlattenAsync()).ToArray();
        }
        catch (HttpException exception) when (exception.HttpCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            return null;
        }

        var accessibleChannels = getAccessibleChannels();
        if (!accessibleChannels.ContainsKey(channelId))
            return null;
        var consumed = messages
            .Where(message => message.Channel.Id == channelId && message.Id > afterMessageId)
            .DistinctBy(message => message.Id)
            .OrderBy(message => message.Id)
            .ToArray();
        var observations = new List<NotebookObservationMessage>();
        foreach (var message in consumed)
        {
            var source = CreateSource(message, channelId, message.Id, accessibleChannels);
            if (source is not null)
                observations.Add(new NotebookObservationMessage(message.Id, source));
        }

        return getAccessibleChannels().ContainsKey(channelId)
            ? new NotebookObservationBatch(channelId, consumed.LastOrDefault()?.Id ?? afterMessageId, observations)
            : null;
    }

    public async Task<IReadOnlyList<NotebookSource>?> ReadSourcesAsync(IReadOnlyList<string> urls, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (urls.Count is < 1 or > 3)
            return null;
        var accessibleChannels = getAccessibleChannels();
        if (accessibleChannels.Count == 0)
            return null;

        var sources = new List<NotebookSource>();
        var sourceChannelIds = new HashSet<ulong>();
        foreach (var url in urls.Distinct(StringComparer.Ordinal))
        {
            accessibleChannels = getAccessibleChannels();
            if (
                !DiscordMessageService.TryParseNotebookSourceUrl(url, guildId, out var channelId, out var messageId)
                || !accessibleChannels.ContainsKey(channelId)
            )
                return null;
            sourceChannelIds.Add(channelId);
            IMessage? message;
            try
            {
                var requestOptions = new RequestOptions { CancelToken = cancellationToken };
                var channel = await GetChannelAsync(channelId, requestOptions);
                if (channel is null)
                    return null;
                message = await channel.GetMessageAsync(messageId, options: requestOptions);
            }
            catch (HttpException exception) when (exception.HttpCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
            {
                return null;
            }

            accessibleChannels = getAccessibleChannels();
            if (!sourceChannelIds.All(accessibleChannels.ContainsKey))
                return null;
            var source = CreateSource(message, channelId, messageId, accessibleChannels);
            if (source is null)
                return null;
            sources.Add(source);
        }

        var currentChannels = getAccessibleChannels();
        return sourceChannelIds.All(currentChannels.ContainsKey) ? sources : null;
    }

    private async Task<ITextChannel?> GetChannelAsync(ulong channelId, RequestOptions requestOptions)
    {
        var channel = await restClient.Client.GetChannelAsync(channelId, options: requestOptions);
        return
            channel is ITextChannel textChannel
            && channel is not (IThreadChannel or IVoiceChannel)
            && textChannel.Id == channelId
            && textChannel.GuildId == guildId
            && getAccessibleChannels().ContainsKey(channelId)
            ? textChannel
            : null;
    }

    private NotebookSource? CreateSource(IMessage? message, ulong channelId, ulong messageId, IReadOnlyDictionary<ulong, string> accessibleChannels)
    {
        if (!DiscordMessageService.IsNotebookEvidenceMessage(message, channelId, messageId))
            return null;
        var content = DiscordMessageService.SanitizeNotebookSourceContent(message.Content, accessibleChannels);
        if (content is null)
            return null;
        var authorName = message.Author.GetName();
        authorName = authorName[..Math.Min(authorName.Length, 80)];
        var mentions = message.MentionedUserIds.Distinct().Select(id => new NotebookMentionedUser(id, getUserName(id))).ToArray();
        return new NotebookSource(
            $"https://discord.com/channels/{guildId}/{channelId}/{messageId}",
            message.Author.Id,
            authorName,
            message.Timestamp.UtcDateTime,
            content
        )
        {
            ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message.Content))),
            ViewHash = NotebookSource.CalculateViewHash(content, authorName, mentions),
            MentionedUsers = mentions,
        };
    }
}
