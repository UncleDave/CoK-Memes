using ChampionsOfKhazad.Bot.GenAi;
using Discord;

namespace ChampionsOfKhazad.Bot;

internal sealed class DiscordSpecialsGateway(DiscordPublicationAudience audience, SharedDiscordRestClient rest) : ISpecialsGateway
{
    internal const int MaximumHistoryReads = 100;
    internal const int MaximumPagesPerChannel = 20;

    public SpecialsDestination? GetDestination() => audience.GetDestination();

    public string DestinationError => audience.DestinationError;

    public string? GetPublicationError(ulong destinationId)
    {
        if (GetDestination()?.Id != destinationId)
            return DestinationError.Length > 0 ? DestinationError : "The specials destination changed.";
        var guild = audience.GetGuild();
        var channel = guild?.GetTextChannel(destinationId);
        if (channel is null || guild?.CurrentUser is not { } bot)
            return "The guild connection is not ready.";
        return GetTextPublicationError(bot.GetPermissions(channel), channel.Name);
    }

    internal static string? GetTextPublicationError(ChannelPermissions permissions, string name) =>
        !permissions.ViewChannel ? $"The bot needs View Channel in #{name}."
        : !permissions.SendMessages ? $"The bot needs Send Messages in #{name}."
        : null;

    public async Task<SpecialsChatBatch> ReadWeekAsync(
        ulong destinationId,
        DateTimeOffset since,
        DateTimeOffset until,
        CancellationToken cancellationToken
    )
    {
        var reader = audience.CreateReader(destinationId);
        var batch = await ReadHistoryAsync(
            reader.GetChannelIds(),
            (id, before, token) => reader.ReadHistoryPageAsync(id, since, until, before, token),
            cancellationToken
        );
        var allowed = audience.GetSourceChannels(destinationId);
        var guildId = audience.GetGuild()?.Id ?? 0;
        if (
            GetDestination()?.Id != destinationId
            || batch.Sources.Any(source =>
                !DiscordMessageService.TryParseNotebookSourceUrl(source.Url, guildId, out var id, out _) || !allowed.ContainsKey(id)
            )
        )
            throw new InvalidOperationException("The publication audience changed during history reads.");
        return batch;
    }

    internal static async Task<SpecialsChatBatch> ReadHistoryAsync(
        IReadOnlyList<ulong> channels,
        Func<ulong, ulong?, CancellationToken, Task<DiscordHistoryPage?>> read,
        CancellationToken cancellationToken
    )
    {
        var queue = new Queue<ulong>(channels.Distinct().Order());
        var sources = new Dictionary<ulong, List<NotebookSource>>();
        var cursors = new Dictionary<ulong, ulong>();
        var pages = new Dictionary<ulong, int>();
        var complete = 0;
        var failures = 0;
        var limited = 0;
        var reads = 0;
        var excluded = 0;
        while (queue.TryDequeue(out var id))
        {
            if (reads == MaximumHistoryReads)
            {
                limited += queue.Count + 1;
                break;
            }
            cancellationToken.ThrowIfCancellationRequested();
            DiscordHistoryPage? page;
            try
            {
                reads++;
                page = await read(id, cursors.TryGetValue(id, out var cursor) ? cursor : null, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                page = null;
            }
            if (page is null || (!page.Complete && (page.Before is null || (cursors.TryGetValue(id, out var previous) && page.Before >= previous))))
            {
                failures++;
                sources.Remove(id);
                continue;
            }
            excluded += page.ExcludedMessages;
            if (!sources.TryGetValue(id, out var collected))
                sources[id] = collected = [];
            collected.AddRange(page.Sources);
            pages[id] = pages.GetValueOrDefault(id) + 1;
            if (page.Complete)
                complete++;
            else if (pages[id] == MaximumPagesPerChannel)
                limited++;
            else
            {
                cursors[id] = page.Before!.Value;
                queue.Enqueue(id);
            }
        }
        return new(
            sources.Values.SelectMany(value => value).DistinctBy(source => source.Url).OrderBy(source => source.TimestampUtc).ToArray(),
            channels.Distinct().Count(),
            complete,
            failures,
            limited,
            reads,
            excluded
        );
    }

    public async Task<IReadOnlyList<NotebookSource>> ResolveNamesAsync(IReadOnlyList<NotebookSource> sources, CancellationToken cancellationToken) =>
        await audience.CreateNameResolver().ResolveAsync(sources, refresh: true, cancellationToken);

    public Task<bool> VerifyAsync(ulong destinationId, IReadOnlyList<NotebookSource> sources, CancellationToken cancellationToken) =>
        audience.VerifySourcesAsync(destinationId, sources, cancellationToken);

    public async Task<ulong> PublishAsync(ulong destinationId, string menu, CancellationToken cancellationToken)
    {
        if (menu.Length > 2000 || GetPublicationError(destinationId) is not null)
            throw new InvalidOperationException("Specials publication is unavailable.");
        var channel = await rest.Client.GetChannelAsync(destinationId, options: new RequestOptions { CancelToken = cancellationToken });
        if (
            channel is not ITextChannel text
            || channel is IThreadChannel or IVoiceChannel
            || text.Id != destinationId
            || text.GuildId != audience.GetGuild()?.Id
            || GetPublicationError(destinationId) is not null
        )
            throw new InvalidOperationException("Specials destination changed.");
        return await SendMenuAsync(text, menu, () => GetPublicationError(destinationId) is null, cancellationToken);
    }

    internal static async Task<ulong> SendMenuAsync(ITextChannel text, string menu, Func<bool> canSend, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(menu) || menu.Length > 2000 || !canSend())
            throw new InvalidOperationException("Specials publication is unavailable.");
        var message = await text.SendMessageAsync(
            menu,
            allowedMentions: AllowedMentions.None,
            options: new RequestOptions { CancelToken = cancellationToken }
        );
        return message.Id;
    }
}
