using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.GenAi;

public class NotebookObserverService(
    INotebookStore store,
    NotebookService notebook,
    INotebookBackgroundSourceReader sourceReader,
    INotebookDiscoverer discoverer,
    IOptions<NotebookObserverOptions> options,
    TimeProvider clock,
    ILogger<NotebookObserverService> logger
)
{
    public const int MaximumScanHistory = 100;
    private const long DiscordEpochMilliseconds = 1420070400000L;
    private const int SnowflakeTimestampShift = 22;
    private readonly NotebookObserverOptions _options = options.Value;

    public async Task<bool> IsEligibleAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return false;
        var state = await store.GetAsync(cancellationToken).WaitAsync(cancellationToken);
        return IsEligible(state, clock.GetUtcNow().UtcDateTime) && sourceReader.GetChannelIds().Count > 0;
    }

    public async Task ObserveAsync(CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(_options.ScanTimeoutSeconds), clock);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var token = operation.Token;
        var reservation = await BeginAsync(token);
        if (reservation is null)
            return;

        var (scanId, initial, selected) = reservation.Value;
        var started = initial.Observer.LastStartedAtUtc!.Value;
        var outcome = NotebookObservationOutcome.Failed;
        var messageCount = 0;
        var proposed = 0;
        var attempted = 0;
        var saved = 0;
        var readFailures = 0;
        try
        {
            var checkpoints = initial.Observer.Checkpoints.ToDictionary(checkpoint => checkpoint.ChannelId);
            var progress = new List<NotebookChannelCheckpoint>();
            var batches = new List<NotebookObservationBatch>();
            var sources = new List<NotebookSource>();
            var sourceUrls = new HashSet<string>(StringComparer.Ordinal);
            var sourceChannels = new Dictionary<string, ulong>(StringComparer.Ordinal);
            var characters = 0;
            var windowStart = started.AddMinutes(-_options.LookbackMinutes);
            foreach (var channelId in selected)
            {
                token.ThrowIfCancellationRequested();
                var current = await store.GetAsync(token).WaitAsync(token);
                var stop = GetStopOutcome(current, scanId, clock.GetUtcNow().UtcDateTime);
                if (stop is { } stopped)
                {
                    outcome = stopped;
                    return;
                }
                var afterId = Math.Max(checkpoints.GetValueOrDefault(channelId)?.LastMessageId ?? 0, GetWindowStartMessageId(windowStart));
                NotebookObservationBatch? batch;
                try
                {
                    batch = await sourceReader.ReadBatchAsync(channelId, afterId, _options.MaximumMessagesPerChannel, token).WaitAsync(token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    readFailures++;
                    logger.LogWarning(exception, "Notebook observation could not read channel {ChannelId}", channelId);
                    continue;
                }
                if (batch is null || batch.ChannelId != channelId || batch.LastMessageId < afterId)
                {
                    readFailures++;
                    continue;
                }
                progress.Add(new NotebookChannelCheckpoint(channelId, batch.LastMessageId, started));
                batches.Add(
                    batch with
                    {
                        Messages = batch
                            .Messages.Where(message => message.MessageId > afterId)
                            .OrderBy(message => message.MessageId)
                            .Take(_options.MaximumMessagesPerChannel)
                            .ToArray(),
                    }
                );
            }
            var readableChannels = sourceReader.GetChannelIds().ToHashSet();
            var rows = batches.Count == 0 ? 0 : batches.Max(batch => batch.Messages.Count);
            for (var row = 0; row < rows; row++)
            {
                foreach (var batch in batches)
                {
                    if (row >= batch.Messages.Count || !readableChannels.Contains(batch.ChannelId))
                        continue;
                    var message = batch.Messages[row];
                    if (
                        message.MessageId > batch.LastMessageId
                        || message.Source.TimestampUtc < windowStart
                        || message.Source.TimestampUtc > started
                        || string.IsNullOrWhiteSpace(message.Source.Content)
                        || message.Source.Content.Length > NotebookSource.MaximumContentLength
                        || sourceUrls.Contains(message.Source.Url)
                    )
                        continue;
                    var size = JsonSerializer.Serialize(message.Source).Length;
                    if (size > _options.MaximumInputCharacters - characters)
                        continue;
                    sources.Add(message.Source);
                    sourceUrls.Add(message.Source.Url);
                    sourceChannels[message.Source.Url] = batch.ChannelId;
                    characters += size;
                }
            }
            sources.RemoveAll(source => !readableChannels.Contains(sourceChannels[source.Url]));
            sourceUrls.IntersectWith(sources.Select(source => source.Url));
            messageCount = sources.Count;
            if (sources.Count == 0)
            {
                outcome =
                    await ConsumeAsync(scanId, initial.ReviewRevision, progress, readableChannels, token)
                    ?? (readFailures > 0 ? NotebookObservationOutcome.ReadFailed : NotebookObservationOutcome.NoMessages);
                return;
            }

            var charge = await ReserveDiscoveryAsync(scanId, initial.ReviewRevision, token);
            if (charge.Error is { } chargeError)
            {
                outcome = chargeError;
                return;
            }
            IReadOnlyList<NotebookProposal> candidates;
            try
            {
                readableChannels = sourceReader.GetChannelIds().ToHashSet();
                sources.RemoveAll(source => !readableChannels.Contains(sourceChannels[source.Url]));
                sourceUrls.IntersectWith(sources.Select(source => source.Url));
                messageCount = sources.Count;
                if (sources.Count == 0)
                {
                    outcome = NotebookObservationOutcome.Interrupted;
                    return;
                }
                candidates = await discoverer.DiscoverAsync(sources, charge.RemainingReviews, token).WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                outcome = NotebookObservationOutcome.DiscoveryFailed;
                logger.LogWarning(exception, "Notebook observation discovery failed");
                return;
            }
            var consumeError = await ConsumeAsync(scanId, initial.ReviewRevision, progress, readableChannels, token);
            if (consumeError is { } error)
            {
                outcome = error;
                return;
            }

            var distinct = candidates
                .Where(candidate => candidate.SourceUrls is { Length: > 0 } && candidate.SourceUrls.All(sourceUrls.Contains))
                .DistinctBy(candidate => candidate.Content, StringComparer.OrdinalIgnoreCase)
                .Take(charge.RemainingReviews)
                .ToArray();
            proposed = distinct.Length;
            outcome = NotebookObservationOutcome.Completed;
            foreach (var candidate in distinct)
            {
                var current = await store.GetAsync(token).WaitAsync(token);
                var stop = GetStopOutcome(current, scanId, clock.GetUtcNow().UtcDateTime);
                if (stop is { } stopped)
                {
                    outcome = stopped;
                    break;
                }
                attempted++;
                var result = await notebook.RememberObservedAsync(candidate, scanId, token).WaitAsync(token);
                if (result.Outcome == NotebookWriteOutcome.Saved)
                    saved++;
                else if (
                    result.Outcome
                    is NotebookWriteOutcome.WriteBlocked
                        or NotebookWriteOutcome.CommitRejected
                        or NotebookWriteOutcome.ReviewDeliveryFailed
                )
                {
                    outcome = NotebookObservationOutcome.Interrupted;
                    break;
                }
                else if (result.Outcome is NotebookWriteOutcome.Failed or NotebookWriteOutcome.TimedOut)
                {
                    outcome =
                        result.Outcome == NotebookWriteOutcome.TimedOut ? NotebookObservationOutcome.TimedOut : NotebookObservationOutcome.Failed;
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = NotebookObservationOutcome.Cancelled;
            throw;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            outcome = NotebookObservationOutcome.TimedOut;
        }
        catch (Exception exception)
        {
            outcome = NotebookObservationOutcome.Failed;
            logger.LogWarning(exception, "Notebook observation failed");
        }
        finally
        {
            await FinishAsync(
                scanId,
                new NotebookObservationScan(started, clock.GetUtcNow().UtcDateTime, outcome, messageCount, proposed, attempted, saved, readFailures)
            );
        }
    }

    private bool IsEligible(NotebookState state, DateTime now) =>
        _options.Enabled
        && notebook.CanRememberBackground(state, now)
        && (state.Observer.ActiveScanId is null || state.Observer.LeaseExpiresAtUtc <= now)
        && (state.Observer.LastCompletedAtUtc is null || state.Observer.LastCompletedAtUtc <= now.AddMinutes(-_options.CooldownMinutes))
        && state.Observer.DiscoveryAttempts.Count(attempt => attempt > now.AddDays(-1)) < _options.DailyDiscoveryLimit;

    private async Task<(string Id, NotebookState State, IReadOnlyList<ulong> Selected)?> BeginAsync(CancellationToken token)
    {
        for (var retry = 0; retry < 10; retry++)
        {
            var current = await store.GetAsync(token).WaitAsync(token);
            var now = clock.GetUtcNow().UtcDateTime;
            if (!IsEligible(current, now))
                return null;
            var channelIds = sourceReader.GetChannelIds().Distinct().ToHashSet();
            if (channelIds.Count == 0)
                return null;
            var checkpoints = current.Observer.Checkpoints.ToDictionary(checkpoint => checkpoint.ChannelId);
            var attempts = current
                .Observer.ChannelAttempts.Where(attempt => channelIds.Contains(attempt.ChannelId))
                .ToDictionary(attempt => attempt.ChannelId);
            var selected = channelIds
                .OrderBy(channelId =>
                    attempts.GetValueOrDefault(channelId)?.AttemptedAtUtc
                    ?? checkpoints.GetValueOrDefault(channelId)?.ScannedAtUtc
                    ?? DateTime.MinValue
                )
                .ThenBy(channelId => attempts.GetValueOrDefault(channelId)?.FirstPriorityAtUtc ?? DateTime.MinValue)
                .ThenBy(channelId => channelId)
                .Take(_options.MaximumChannelsPerScan)
                .ToArray();
            foreach (var channelId in selected)
                attempts[channelId] = new NotebookChannelAttempt(channelId, now)
                {
                    FirstPriorityAtUtc = channelId == selected[0] ? now : attempts.GetValueOrDefault(channelId)?.FirstPriorityAtUtc,
                };
            var id = Guid.NewGuid().ToString("N");
            var updated = current with
            {
                Observer = current.Observer with
                {
                    ActiveScanId = id,
                    LastStartedAtUtc = now,
                    LeaseExpiresAtUtc = now.AddSeconds(_options.ScanTimeoutSeconds),
                    ChannelAttempts = attempts.Values.OrderBy(attempt => attempt.ChannelId).ToArray(),
                },
            };
            if (await store.TrySaveAsync(updated, token).WaitAsync(token))
                return (id, updated, selected);
        }
        return null;
    }

    private NotebookObservationOutcome? GetStopOutcome(NotebookState state, string scanId, DateTime now)
    {
        if (state.Paused || !_options.Enabled)
            return NotebookObservationOutcome.Paused;
        if (state.Observer.ActiveScanId != scanId || state.Observer.LeaseExpiresAtUtc is not { } expiry || expiry <= now)
            return NotebookObservationOutcome.Interrupted;
        return notebook.CanRememberBackground(state, now) ? null : NotebookObservationOutcome.BudgetExhausted;
    }

    private async Task<(int RemainingReviews, NotebookObservationOutcome? Error)> ReserveDiscoveryAsync(
        string scanId,
        long reviewRevision,
        CancellationToken token
    )
    {
        for (var retry = 0; retry < 10; retry++)
        {
            var current = await store.GetAsync(token).WaitAsync(token);
            var now = clock.GetUtcNow().UtcDateTime;
            var error = GetStopOutcome(current, scanId, now);
            if (error is not null)
                return (0, error);
            if (current.ReviewRevision != reviewRevision)
                return (0, NotebookObservationOutcome.Interrupted);
            var recent = current.Observer.DiscoveryAttempts.Where(attempt => attempt > now.AddDays(-1)).ToArray();
            if (recent.Length >= _options.DailyDiscoveryLimit)
                return (0, NotebookObservationOutcome.BudgetExhausted);
            var updated = current with { Observer = current.Observer with { DiscoveryAttempts = [.. recent, now] } };
            if (await store.TrySaveAsync(updated, token).WaitAsync(token))
                return (notebook.GetRemainingBackgroundReviews(updated, now), null);
        }
        return (0, NotebookObservationOutcome.Interrupted);
    }

    private async Task<NotebookObservationOutcome?> ConsumeAsync(
        string scanId,
        long reviewRevision,
        IReadOnlyList<NotebookChannelCheckpoint> progress,
        IReadOnlySet<ulong> channelIds,
        CancellationToken token
    )
    {
        for (var retry = 0; retry < 10; retry++)
        {
            var current = await store.GetAsync(token).WaitAsync(token);
            var error = GetStopOutcome(current, scanId, clock.GetUtcNow().UtcDateTime);
            if (error is not null)
                return error;
            if (current.ReviewRevision != reviewRevision)
                return NotebookObservationOutcome.Interrupted;
            var checkpoints = current
                .Observer.Checkpoints.Where(checkpoint => channelIds.Contains(checkpoint.ChannelId))
                .ToDictionary(checkpoint => checkpoint.ChannelId);
            foreach (var checkpoint in progress.Where(checkpoint => channelIds.Contains(checkpoint.ChannelId)))
            {
                var previous = checkpoints.GetValueOrDefault(checkpoint.ChannelId);
                checkpoints[checkpoint.ChannelId] = checkpoint with
                {
                    LastMessageId = Math.Max(previous?.LastMessageId ?? 0, checkpoint.LastMessageId),
                };
            }
            var updated = current with
            {
                Observer = current.Observer with { Checkpoints = checkpoints.Values.OrderBy(checkpoint => checkpoint.ChannelId).ToArray() },
            };
            if (await store.TrySaveAsync(updated, token).WaitAsync(token))
                return null;
        }
        return NotebookObservationOutcome.Interrupted;
    }

    private async Task FinishAsync(string scanId, NotebookObservationScan scan)
    {
        using var deadline = new CancellationTokenSource(NotebookService.DiagnosticTimeout, clock);
        try
        {
            for (var retry = 0; retry < 10; retry++)
            {
                var current = await store.GetAsync(deadline.Token).WaitAsync(deadline.Token);
                if (current.Observer.ActiveScanId != scanId)
                    return;
                var updated = current with
                {
                    Observer = current.Observer with
                    {
                        ActiveScanId = null,
                        LeaseExpiresAtUtc = null,
                        LastCompletedAtUtc = scan.CompletedAtUtc,
                        Scans = current
                            .Observer.Scans.Append(scan)
                            .Where(item => item.CompletedAtUtc > scan.CompletedAtUtc.AddDays(-1))
                            .OrderBy(item => item.CompletedAtUtc)
                            .TakeLast(MaximumScanHistory)
                            .ToArray(),
                    },
                };
                if (await store.TrySaveAsync(updated, deadline.Token).WaitAsync(deadline.Token))
                    return;
            }
            logger.LogWarning("Notebook observation completion could not be recorded");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Notebook observation completion could not be recorded");
        }
    }

    internal static ulong GetWindowStartMessageId(DateTime utc) =>
        (ulong)Math.Max(0, new DateTimeOffset(utc).ToUnixTimeMilliseconds() - DiscordEpochMilliseconds) << SnowflakeTimestampShift;
}
