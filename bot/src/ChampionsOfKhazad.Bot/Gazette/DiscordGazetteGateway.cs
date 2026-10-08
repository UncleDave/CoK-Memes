using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

internal sealed class DiscordGazetteGateway(
    BotContextProvider contextProvider,
    SharedDiscordRestClient rest,
    IOptions<GazetteOptions> options,
    IOptions<DiscordMessageToolsOptions> messageOptions,
    IOptions<DirectMessageHandlerOptions> adminOptions
) : IGazetteGateway
{
    internal const int MaximumChannels = 12;
    internal const int MaximumMessagesPerChannel = 100;
    internal const int MaximumInputCharacters = 40000;
    internal const int MaximumSources = 160;

    public GazetteDestination? GetDestination()
    {
        var guild = GetGuild();
        if (guild is null)
            return null;
        var matches = guild
            .TextChannels.Where(channel => channel is not (SocketThreadChannel or SocketVoiceChannel) && !channel.IsNsfw)
            .Where(channel =>
                options.Value.DestinationChannelId != 0
                    ? channel.Id == options.Value.DestinationChannelId
                    : channel.Name.Equals(options.Value.DestinationChannelName, StringComparison.OrdinalIgnoreCase)
            )
            .ToArray();
        if (matches.Length != 1)
            return null;
        var destination = matches[0];
        var role = guild.GetRole(messageOptions.Value.NormalUserRoleId);
        var admin = guild.GetUser(adminOptions.Value.AdminUserId);
        var bot = guild.CurrentUser;
        if (
            role is null
            || admin is null
            || bot is null
            || !NormalUserChannelAccess.CanRead(destination, [guild.EveryoneRole, role])
            || !CanRead(admin, destination)
            || !CanRead(bot, destination)
        )
            return null;
        var permissions = bot.GetPermissions(destination);
        return permissions is { SendMessages: true, EmbedLinks: true } ? new(destination.Id, destination.Name) : null;
    }

    public async Task<GazetteChatBatch> ReadRecentAsync(
        ulong destinationId,
        DateTimeOffset since,
        DateTimeOffset until,
        CancellationToken cancellationToken
    )
    {
        var reader = CreateReader(destinationId);
        var ids = reader.GetChannelIds();
        var collected = new List<NotebookSource>();
        var read = 0;
        var failures = 0;
        foreach (var id in ids.Take(MaximumChannels))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var batch = await reader.ReadRecentBatchAsync(id, MaximumMessagesPerChannel, cancellationToken);
                if (batch is null)
                    failures++;
                else
                {
                    read++;
                    collected.AddRange(batch.Messages.Select(message => message.Source));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                failures++;
            }
        }
        var channels = GetSourceChannels(destinationId);
        var sources = SelectSources(
            collected.Where(source =>
                DiscordMessageService.TryParseNotebookSourceUrl(source.Url, GetGuild()?.Id ?? 0, out var channelId, out _)
                && channels.ContainsKey(channelId)
            ),
            since,
            until
        );
        return new(sources, read, ids.Count, failures);
    }

    internal static IReadOnlyList<NotebookSource> SelectSources(IEnumerable<NotebookSource> sources, DateTimeOffset since, DateTimeOffset until)
    {
        var selected = new List<NotebookSource>();
        var characters = 0;
        foreach (
            var source in sources
                .Where(source => source.TimestampUtc >= since.UtcDateTime && source.TimestampUtc <= until.UtcDateTime)
                .DistinctBy(source => source.Url)
                .OrderByDescending(source => source.TimestampUtc)
        )
        {
            // Include metadata allowance; keep complete messages rather than truncating away qualifications/corrections.
            var size = source.Content.Length + source.AuthorName.Length + source.Url.Length + source.MentionedUsers.Count * 100 + 200;
            if (characters + size > MaximumInputCharacters || selected.Count == MaximumSources)
                break;
            selected.Add(source);
            characters += size;
        }
        return selected.OrderBy(source => source.TimestampUtc).ToArray();
    }

    public async Task<bool> VerifyAsync(ulong destinationId, IReadOnlyList<NotebookSource> sources, CancellationToken cancellationToken)
    {
        if (sources.Count is < 1 or > 9 || GetDestination()?.Id != destinationId)
            return false;
        var reader = CreateReader(destinationId);
        foreach (var group in sources.Chunk(3))
        {
            var current = await reader.ReadSourcesAsync(group.Select(source => source.Url).ToArray(), cancellationToken);
            if (!SourcesMatch(group, current))
                return false;
        }
        var allowed = GetSourceChannels(destinationId);
        var guild = GetGuild();
        return guild is not null
            && sources.All(source =>
                DiscordMessageService.TryParseNotebookSourceUrl(source.Url, guild.Id, out var id, out _) && allowed.ContainsKey(id)
            );
    }

    internal static bool SourcesMatch(IReadOnlyList<NotebookSource> expected, IReadOnlyList<NotebookSource>? current) =>
        current is not null
        && current.Count == expected.Count
        && expected.All(source =>
            current.Any(actual =>
                actual.Url == source.Url
                && actual.AuthorId == source.AuthorId
                && actual.TimestampUtc == source.TimestampUtc
                && actual.ContentHash == source.ContentHash
                && actual.ViewHash == source.ViewHash
            )
        );

    public async Task<ulong> PublishAsync(ulong destinationId, string edition, CancellationToken cancellationToken)
    {
        if (edition.Length > 4000 || GetDestination()?.Id != destinationId)
            throw new InvalidOperationException("Gazette destination is unavailable.");
        var channel = await rest.Client.GetChannelAsync(destinationId, options: new RequestOptions { CancelToken = cancellationToken });
        if (
            channel is not ITextChannel text
            || channel is IThreadChannel or IVoiceChannel
            || text.Id != destinationId
            || text.GuildId != GetGuild()?.Id
            || GetDestination()?.Id != destinationId
        )
            throw new InvalidOperationException("Gazette destination changed.");
        var message = await text.SendMessageAsync(
            embed: new EmbedBuilder().WithDescription(edition).Build(),
            allowedMentions: AllowedMentions.None,
            options: new RequestOptions { CancelToken = cancellationToken }
        );
        return message.Id;
    }

    private DiscordNotebookSourceReader CreateReader(ulong destinationId) =>
        new(rest, GetGuild()?.Id ?? 0, () => GetSourceChannels(destinationId), id => GetGuild()?.GetUser(id)?.GetName());

    private IReadOnlyDictionary<ulong, string> GetSourceChannels(ulong destinationId)
    {
        var guild = GetGuild();
        if (guild is null || GetDestination()?.Id != destinationId)
            return new Dictionary<ulong, string>();
        var normalRole = guild.GetRole(messageOptions.Value.NormalUserRoleId);
        var requester = guild.GetUser(adminOptions.Value.AdminUserId);
        var bot = guild.CurrentUser;
        if (normalRole is null || requester is null || bot is null)
            return new Dictionary<ulong, string>();
        var channels = guild
            .TextChannels.Where(channel => channel is not (SocketThreadChannel or SocketVoiceChannel) && !channel.IsNsfw)
            .Where(channel => options.Value.SourceChannelIds.Length == 0 || options.Value.SourceChannelIds.Contains(channel.Id))
            .ToArray();
        var destination = guild.GetTextChannel(destinationId);
        var everyoneCanRead = NormalUserChannelAccess.CanRead(destination, [guild.EveryoneRole]);
        var candidates = channels.Select(channel => new DiscordMessageAccessPolicy.ChannelCandidate(
            channel.Id,
            NormalUserChannelAccess.CanRead(channel, [guild.EveryoneRole, normalRole]),
            NormalUserChannelAccess.CanRead(channel, [guild.EveryoneRole]),
            CanRead(requester, channel),
            CanRead(bot, channel)
        ));
        var allowed = DiscordMessageAccessPolicy.GetGazetteSourceChannelIds(candidates, everyoneCanRead);
        return channels.Where(channel => allowed.Contains(channel.Id)).ToDictionary(channel => channel.Id, channel => channel.Name);
    }

    private SocketGuild? GetGuild() =>
        contextProvider.IsReady
        && contextProvider.BotContext is { Guild: SocketGuild guild } context
        && context.Client.ConnectionState == ConnectionState.Connected
        && ReferenceEquals(context.Client.GetGuild(guild.Id), guild)
            ? guild
            : null;

    private static bool CanRead(SocketGuildUser user, SocketGuildChannel channel) =>
        user.GetPermissions(channel) is { ViewChannel: true, ReadMessageHistory: true };
}
