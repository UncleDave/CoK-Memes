using System.Net;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

internal sealed class DiscordPublicationAudience(
    BotContextProvider contextProvider,
    SharedDiscordRestClient rest,
    IOptions<SpecialsOptions> options,
    IOptions<DiscordMessageToolsOptions> messageOptions
)
{
    public string DestinationError { get; private set; } = "The specials destination is unavailable.";

    public SpecialsDestination? GetDestination()
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
                    ? $"The configured specials channel ID {options.Value.DestinationChannelId} was not found in this guild."
                    : $"No text channel has the exact configured name '{options.Value.DestinationChannelName}'. Configure Specials:DestinationChannelId instead."
            );
        if (matches.Length > 1)
            return Unavailable("The specials channel name is ambiguous. Configure Specials:DestinationChannelId to select exactly one channel.");
        var destination = matches[0];
        if (destination.IsNsfw)
            return Unavailable($"#{destination.Name} is marked NSFW; specials require a non-NSFW destination.");
        var role = guild.GetRole(messageOptions.Value.NormalUserRoleId);
        if (role is null)
            return Unavailable("The configured normal-member role was not found. Check DiscordMessageTools:NormalUserRoleId.");
        if (!NormalUserChannelAccess.CanRead(destination, [guild.EveryoneRole, role]))
            return Unavailable($"The normal-member role cannot both View Channel and Read Message History in #{destination.Name}.");
        DestinationError = string.Empty;
        return new(destination.Id, destination.Name);
    }

    private SpecialsDestination? Unavailable(string reason)
    {
        DestinationError = reason;
        return null;
    }

    internal async Task<bool> VerifySourcesAsync(ulong destinationId, IReadOnlyList<NotebookSource> sources, CancellationToken cancellationToken)
    {
        if (sources.Count is < 1 or > 15 || GetDestination()?.Id != destinationId)
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

    internal DiscordNotebookSourceReader CreateReader(ulong destinationId) =>
        new(rest, GetGuild()?.Id ?? 0, () => GetSourceChannels(destinationId), id => GetGuild()?.GetUser(id)?.GetName());

    internal DiscordMemberNameResolver CreateNameResolver()
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
                    return (await memberGuild.GetUserAsync(id, options: request))?.GetName();
                }
                catch (HttpException exception) when (exception.HttpCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
            },
            id => GetGuild()?.GetUser(id)?.GetName()
        );
    }

    internal IReadOnlyDictionary<ulong, string> GetSourceChannels(ulong destinationId)
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
            BotCanRead: bot.GetPermissions(channel) is { ViewChannel: true, ReadMessageHistory: true }
        ));
        var allowed = DiscordMessageAccessPolicy.GetPublicationSourceChannelIds(candidates, everyoneCanRead);
        return channels.Where(channel => allowed.Contains(channel.Id)).ToDictionary(channel => channel.Id, channel => channel.Name);
    }

    internal SocketGuild? GetGuild() =>
        contextProvider.IsReady
        && contextProvider.BotContext is { Guild: SocketGuild guild } context
        && context.Client.ConnectionState == ConnectionState.Connected
        && ReferenceEquals(context.Client.GetGuild(guild.Id), guild)
            ? guild
            : null;
}
