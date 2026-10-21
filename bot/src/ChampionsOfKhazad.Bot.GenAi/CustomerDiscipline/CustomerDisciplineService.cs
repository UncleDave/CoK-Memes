namespace ChampionsOfKhazad.Bot.GenAi;

public class CustomerDisciplineService(ICustomerDisciplineStore store, TimeProvider timeProvider)
{
    public async Task<CustomerDisciplineState> GetAsync(ulong guildId, ulong userId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(guildId);
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        var empty = CustomerDisciplineState.Create(guildId, userId);
        return await store.GetAsync(empty.Id, cancellationToken) ?? empty;
    }

    public Task<CustomerDisciplineResult> IssueStrikeAsync(
        ulong guildId,
        ulong userId,
        ulong messageId,
        string reason,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentOutOfRangeException.ThrowIfZero(messageId);
        reason = reason.Trim();
        if (reason.Length is 0 or > 200)
            return Task.FromResult(new CustomerDisciplineResult(false, null, "A strike needs a reason of 1–200 characters."));

        return UpdateAsync(
            guildId,
            userId,
            state =>
                state.IsBarred || messageId <= state.LastPardonMessageId || state.Strikes.Any(strike => strike.MessageId == messageId)
                    ? state
                    : state with
                    {
                        Strikes = [.. state.Strikes, new CustomerStrike(messageId, reason, timeProvider.GetUtcNow().UtcDateTime)],
                    },
            cancellationToken
        );
    }

    public Task<CustomerDisciplineResult> PardonAsync(ulong guildId, ulong userId, ulong messageId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(messageId);
        return UpdateAsync(
            guildId,
            userId,
            state =>
                messageId <= state.LastPardonMessageId || state.Strikes.Any(strike => strike.MessageId >= messageId)
                    ? state
                    : state with
                    {
                        Strikes = [],
                        LastPardonMessageId = messageId,
                    },
            cancellationToken
        );
    }

    private async Task<CustomerDisciplineResult> UpdateAsync(
        ulong guildId,
        ulong userId,
        Func<CustomerDisciplineState, CustomerDisciplineState> update,
        CancellationToken cancellationToken
    )
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = await GetAsync(guildId, userId, cancellationToken);
            var updated = update(state);
            if (ReferenceEquals(state, updated))
                return new CustomerDisciplineResult(false, state);
            if (await store.TrySaveAsync(updated, cancellationToken))
                return new CustomerDisciplineResult(updated.Strikes.Length != state.Strikes.Length, updated with { Revision = updated.Revision + 1 });
        }

        return new CustomerDisciplineResult(false, null, "The customer ledger is busy. Try again later.");
    }
}
