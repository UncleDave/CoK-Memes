namespace ChampionsOfKhazad.Bot.GenAi;

public sealed class GazetteIssueService(IGazetteIssueStore store, TimeProvider clock)
{
    public async Task<long> GetNextAsync(CancellationToken cancellationToken) =>
        checked((await store.GetAsync(cancellationToken)).LastReservedIssue + 1);

    public async Task<bool> TryReserveAsync(long number, string token, ulong channelId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var state = await store.GetAsync(cancellationToken);
            if (number != checked(state.LastReservedIssue + 1))
                return false;
            var publication = new GazettePublication(number, token, channelId, clock.GetUtcNow().UtcDateTime);
            if (
                await store.TrySaveAsync(
                    state with
                    {
                        LastReservedIssue = number,
                        Publications = state.Publications.TakeLast(99).Append(publication).ToArray(),
                    },
                    cancellationToken
                )
            )
                return true;
        }
        return false;
    }

    public async Task MarkPublishedAsync(long number, string token, ulong messageId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var state = await store.GetAsync(cancellationToken);
            var publication = state.Publications.SingleOrDefault(item => item.Number == number && item.Token == token);
            if (publication is null || (publication.MessageId is { } existing && existing != messageId))
                throw new InvalidOperationException("Gazette publication reservation changed.");
            if (publication.MessageId == messageId)
                return;
            if (
                await store.TrySaveAsync(
                    state with
                    {
                        Publications = state.Publications.Select(item => item == publication ? item with { MessageId = messageId } : item).ToArray(),
                    },
                    cancellationToken
                )
            )
                return;
        }
        throw new InvalidOperationException("Gazette publication acknowledgement could not be saved.");
    }
}
