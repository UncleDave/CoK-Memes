using System.Globalization;
using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

public class CustomerDirectMessageCommand(
    CustomerDisciplineService disciplineService,
    BotContext botContext,
    ILogger<CustomerDirectMessageCommand> logger
)
{
    private const string HelpText =
        "Commands: customers | customers show <user-id> | customers pardon <user-id>. User IDs or Discord user mentions are accepted. Three strikes: no service until pardoned.";

    public async Task<string?> ExecuteAsync(string content, ulong messageId, CancellationToken cancellationToken = default)
    {
        var parts = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !parts[0].Equals("customers", StringComparison.OrdinalIgnoreCase))
            return null;

        if (
            parts.Length != 3
            || !(parts[1].Equals("show", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("pardon", StringComparison.OrdinalIgnoreCase))
            || !TryParseUserId(parts[2], out var userId)
        )
            return HelpText;

        try
        {
            if (parts[1].Equals("show", StringComparison.OrdinalIgnoreCase))
                return Describe(userId, await disciplineService.GetAsync(botContext.Guild.Id, userId, cancellationToken));

            var result = await disciplineService.PardonAsync(botContext.Guild.Id, userId, messageId, cancellationToken);
            if (result.Error is not null || result.State is null)
                return $"Customer pardon could not be confirmed. Check customers show {userId} before retrying.";

            return result.Changed ? $"Customer {userId} pardoned. {DescribeService(result.State)}" : Describe(userId, result.State);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Customer admin command failed");
            return $"Customer command failed; its effect could not be confirmed. Check customers show {userId} before retrying.";
        }
    }

    private static bool TryParseUserId(string text, out ulong userId)
    {
        if (text.StartsWith("<@", StringComparison.Ordinal) && text.EndsWith('>'))
        {
            text = text[2..^1];
            if (text.StartsWith('!'))
                text = text[1..];
        }

        return ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out userId) && userId != 0;
    }

    private static string Describe(ulong userId, CustomerDisciplineState state)
    {
        var status = $"Customer {userId}: {DescribeService(state)}";
        if (state.Strikes.Length == 0)
            return status;

        var strikes = state.Strikes.Select(strike =>
            $"{strike.AtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)} — {strike.Reason} (message {strike.MessageId})"
        );
        return status + "\n" + string.Join('\n', strikes);
    }

    private static string DescribeService(CustomerDisciplineState state) =>
        $"{state.Strikes.Length}/3 strikes. " + (state.IsBarred ? "Service refused until pardoned." : "Service available.");
}
