using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class CustomerDisciplineRequest(
    CustomerDisciplineService service,
    IMessageContext context,
    CustomerDisciplineState initialState,
    ILogger logger
)
{
    private int _changeAttempts;

    public bool StartedBarred { get; } = initialState.IsBarred;
    public CustomerDisciplineResult? Change { get; private set; }
    public string? Error { get; private set; }

    public Task<string> IssueStrikeAsync(
        [Description(
            "Concrete direct taunt at Grub or explicit shop misconduct in this customer's current message, 1–200 characters. Not ordinary criticism or bot nostalgia; no 'playful' disclaimers or sensitive details."
        )]
            string reason,
        CancellationToken cancellationToken
    ) =>
        ChangeAsync(
            () => service.IssueStrikeAsync(context.GuildId!.Value, context.UserId, context.MessageId!.Value, reason, cancellationToken),
            cancellationToken
        );

    public Task<string> PardonAsync(CancellationToken cancellationToken) =>
        ChangeAsync(
            () => service.PardonAsync(context.GuildId!.Value, context.UserId, context.MessageId!.Value, cancellationToken),
            cancellationToken
        );

    private async Task<string> ChangeAsync(Func<Task<CustomerDisciplineResult>> change, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _changeAttempts) != 1)
            return "Only one strike or pardon attempt is allowed per request. Do not retry.";

        try
        {
            Change = await change();
            if (Change.Error is { } error)
            {
                Error = error;
                return error;
            }
            var state = Change.State!;
            return $"Customer ledger: {state.Strikes.Length}/3 strikes; barred: {state.IsBarred}; changed: {Change.Changed}. The application delivers any saved verdict separately; do not repeat it in your reply. Never claim a change if changed is false.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to update Grub customer status for Discord user {UserId}", context.UserId);
            Error = "Couldn't confirm the ledger update. Try me again later, or ask the admin to check your strikes.";
            return "The customer ledger update could not be confirmed. Do not claim success or that nothing changed.";
        }
    }
}
