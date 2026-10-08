using System.Net;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

internal sealed class DiscordGazetteGateway(
    BotContextProvider contextProvider,
    SharedDiscordRestClient rest,
    IOptions<GazetteOptions> options,
    IOptions<DiscordMessageToolsOptions> messageOptions,
    IGazettePublishedEditionStore publishedEditions,
    TimeProvider clock
) : IGazetteGateway
{
    internal const int MaximumChannels = 12;
    internal const int MaximumMessagesPerChannel = 100;
    internal const int MaximumInputCharacters = 40000;
    internal const int MaximumSources = 160;

    public string DestinationError { get; private set; } = "The Gazette destination is unavailable.";

    public GazetteDestination? GetDestination()
    {
        var guild = GetGuild();
        if (guild is null)
            return Unavailable("The bot's guild connection is not ready. Try again once it has connected.");
        var matches = guild
            .TextChannels.Where(channel => channel is not (SocketThreadChannel or SocketVoiceChannel))
            .Where(channel =>
                options.Value.DestinationChannelId != 0
                    ? channel.Id == options.Value.DestinationChannelId
                    : channel.Name.Equals(options.Value.DestinationChannelName, StringComparison.OrdinalIgnoreCase)
            )
            .ToArray();
        if (matches.Length == 0)
            return Unavailable(
                options.Value.DestinationChannelId != 0
                    ? $"The configured Gazette channel ID {options.Value.DestinationChannelId} was not found in this guild."
                    : $"No text channel has the exact configured name '{options.Value.DestinationChannelName}'. Configure Gazette:DestinationChannelId instead."
            );
        if (matches.Length > 1)
            return Unavailable("The Gazette channel name is ambiguous. Configure Gazette:DestinationChannelId to select exactly one channel.");
        var destination = matches[0];
        if (destination.IsNsfw)
            return Unavailable($"#{destination.Name} is marked NSFW; the Gazette currently requires a non-NSFW destination.");
        var role = guild.GetRole(messageOptions.Value.NormalUserRoleId);
        if (role is null)
            return Unavailable("The configured normal-member role was not found. Check DiscordMessageTools:NormalUserRoleId.");
        if (!NormalUserChannelAccess.CanRead(destination, [guild.EveryoneRole, role]))
            return Unavailable($"The normal-member role cannot both View Channel and Read Message History in #{destination.Name}.");
        DestinationError = string.Empty;
        return new(destination.Id, destination.Name);
    }

    public string? GetPublicationError(ulong destinationId)
    {
        if (GetDestination()?.Id != destinationId)
            return DestinationError.Length > 0 ? DestinationError : "The Gazette destination changed.";
        var guild = GetGuild();
        var destination = guild?.GetTextChannel(destinationId);
        if (destination is null || guild?.CurrentUser is not { } bot)
            return "The bot's guild connection is not ready for publication.";
        return GetPublicationPermissionError(bot.GetPermissions(destination), destination.Name);
    }

    internal static string? GetPublicationPermissionError(ChannelPermissions permissions, string channelName)
    {
        if (!permissions.ViewChannel)
            return $"The bot needs View Channel in #{channelName}.";
        if (!permissions.SendMessages)
            return $"The bot needs Send Messages in #{channelName}.";
        if (!permissions.EmbedLinks)
            return $"The bot needs Embed Links in #{channelName}.";
        if (!permissions.AttachFiles)
            return $"The bot needs Attach Files in #{channelName} to publish the newspaper page.";
        return null;
    }

    private GazetteDestination? Unavailable(string reason)
    {
        DestinationError = reason;
        return null;
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
        sources = await CreateNameResolver().ResolveAsync(sources, refresh: false, cancellationToken);
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
            if (current is not null)
                current = await CreateNameResolver().ResolveAsync(current, refresh: true, cancellationToken);
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

    public async Task<ulong> PublishAsync(
        ulong destinationId,
        string edition,
        GazettePrintEdition printEdition,
        string publicationId,
        CancellationToken cancellationToken
    )
    {
        if (edition.Length > 4000 || GetDestination()?.Id != destinationId)
            throw new InvalidOperationException("Gazette destination is unavailable.");
        if (GetPublicationError(destinationId) is { } error)
            throw new InvalidOperationException(error);
        var channel = await rest.Client.GetChannelAsync(destinationId, options: new RequestOptions { CancelToken = cancellationToken });
        if (
            channel is not ITextChannel text
            || channel is IThreadChannel or IVoiceChannel
            || text.Id != destinationId
            || text.GuildId != GetGuild()?.Id
            || GetDestination()?.Id != destinationId
            || GetPublicationError(destinationId) is not null
        )
            throw new InvalidOperationException("Gazette destination changed.");
        return await SendPublicationAsync(
            text,
            edition,
            printEdition,
            publicationId,
            () => GetDestination()?.Id == destinationId && GetPublicationError(destinationId) is null,
            cancellationToken
        );
    }

    internal async Task<ulong> SendPublicationAsync(
        ITextChannel text,
        string edition,
        GazettePrintEdition printEdition,
        string publicationId,
        Func<bool> canSend,
        CancellationToken cancellationToken
    )
    {
        if (
            printEdition.Pages.Count is < 1 or > 2
            || printEdition.Pages.Select(page => page.FileName).Distinct().Count() != printEdition.Pages.Count
            || printEdition.Pages.Sum(page => (long)page.Png.Length) > 8_000_000
        )
            throw new InvalidOperationException("Invalid Gazette page set.");
        var components = GazetteReadButton.Build(publicationId);
        await publishedEditions.SaveAsync(new(publicationId, text.GuildId, text.Id, edition, clock.GetUtcNow().UtcDateTime), cancellationToken);
        if (!canSend())
            throw new InvalidOperationException("Gazette destination changed while preparing publication.");
        var streams = printEdition.Pages.Select(page => new MemoryStream(page.Png, writable: false)).ToArray();
        IUserMessage message;
        try
        {
            message = await text.SendFilesAsync(
                printEdition.Pages.Select((page, index) => new FileAttachment(streams[index], page.FileName)).ToArray(),
                embeds: printEdition
                    .Pages.Select(
                        (page, index) =>
                            new EmbedBuilder()
                                .WithTitle(index == 0 ? "The Khazad Gazette — front page" : $"The Khazad Gazette — page {index + 1}")
                                .WithImageUrl($"attachment://{page.FileName}")
                                .Build()
                    )
                    .ToArray(),
                components: components,
                allowedMentions: AllowedMentions.None,
                options: new RequestOptions { CancelToken = cancellationToken }
            );
        }
        finally
        {
            foreach (var stream in streams)
                stream.Dispose();
        }
        await publishedEditions.ConfirmMessageAsync(publicationId, message.Id, cancellationToken);
        return message.Id;
    }

    private DiscordNotebookSourceReader CreateReader(ulong destinationId) =>
        new(rest, GetGuild()?.Id ?? 0, () => GetSourceChannels(destinationId), id => GetGuild()?.GetUser(id)?.GetName());

    private GazetteMemberNameResolver CreateNameResolver()
    {
        IGuild? memberGuild = null;
        return new(
            async (id, cancellationToken) =>
            {
                var guildId = GetGuild()?.Id ?? throw new InvalidOperationException("Guild connection is unavailable.");
                var request = new RequestOptions { CancelToken = cancellationToken };
                memberGuild ??= await rest.Client.GetGuildAsync(guildId, options: request);
                try
                {
                    var member = await memberGuild.GetUserAsync(id, options: request);
                    return member?.GetName();
                }
                catch (HttpException exception) when (exception.HttpCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
            },
            id => GetGuild()?.GetUser(id)?.GetName()
        );
    }

    private IReadOnlyDictionary<ulong, string> GetSourceChannels(ulong destinationId)
    {
        var guild = GetGuild();
        if (guild is null || GetDestination()?.Id != destinationId)
            return new Dictionary<ulong, string>();
        var normalRole = guild.GetRole(messageOptions.Value.NormalUserRoleId);
        var bot = guild.CurrentUser;
        if (normalRole is null || bot is null)
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
            RequesterCanRead: false,
            BotCanRead: CanRead(bot, channel)
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
