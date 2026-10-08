using System.Text;
using System.Text.RegularExpressions;
using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public sealed partial class GazetteDirectMessageCommand(
    IGazetteGateway gateway,
    IGazetteWriter writer,
    GazetteSession session,
    IOptions<DirectMessageHandlerOptions> adminOptions,
    TimeProvider clock,
    ILogger<GazetteDirectMessageCommand> logger
)
{
    private const string Help =
        "Gazette commands: `gazette draft` — privately draft from up to seven days of recent member-readable chat; "
        + "`gazette show` — repeat your pending preview; `gazette approve <token>` — publish the exact preview to #ai-tavern; "
        + "`gazette discard` — clear it. Approval expires after 30 minutes or a restart. Nothing is posted automatically.";

    public async Task<bool> TryExecuteAsync(
        ulong actorId,
        string content,
        Func<string, CancellationToken, Task> reply,
        CancellationToken cancellationToken
    )
    {
        var parts = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (actorId != adminOptions.Value.AdminUserId || parts.Length == 0 || !parts[0].Equals("gazette", StringComparison.OrdinalIgnoreCase))
            return false;

        await session.Gate.WaitAsync(cancellationToken);
        var publishing = false;
        var drafting = parts.Length == 2 && parts[1].Equals("draft", StringComparison.OrdinalIgnoreCase);
        try
        {
            if (session.Pending is { } old && clock.GetUtcNow() >= old.ExpiresAtUtc)
                session.Pending = null;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            var token = timeout.Token;
            if (drafting)
            {
                await DraftAsync(reply, token);
            }
            else if (parts.Length == 2 && parts[1].Equals("discard", StringComparison.OrdinalIgnoreCase))
            {
                session.Pending = null;
                await reply("Gazette draft discarded.", token);
            }
            else if (parts.Length == 2 && parts[1].Equals("show", StringComparison.OrdinalIgnoreCase))
            {
                await reply(session.Pending is { } pending ? Preview(pending) : NoDraft, token);
            }
            else if (parts.Length == 3 && parts[1].Equals("approve", StringComparison.OrdinalIgnoreCase))
            {
                var pending = session.Pending;
                if (pending is null)
                    await reply(NoDraft, token);
                else if (!pending.Token.Equals(parts[2], StringComparison.Ordinal))
                    await reply("Use the exact `gazette approve <token>` from your preview. Nothing was published.", token);
                else
                {
                    // Consume approval before any network work. An ambiguous send must never be automatically retried.
                    session.Pending = null;
                    if (
                        gateway.GetDestination()?.Id != pending.Destination.Id
                        || !await gateway.VerifyAsync(pending.Destination.Id, pending.Sources, token)
                        || clock.GetUtcNow() >= pending.ExpiresAtUtc
                    )
                    {
                        await reply(
                            "The destination or cited evidence changed, became unavailable, or approval expired. Nothing was published. Request `gazette draft` again.",
                            token
                        );
                    }
                    else if (gateway.GetPublicationError(pending.Destination.Id) is { } error)
                    {
                        await reply(
                            $"Cannot publish this edition: {error} The approval was cleared; request a fresh draft after fixing the bot's posting permissions.",
                            token
                        );
                    }
                    else
                    {
                        publishing = true;
                        var messageId = await gateway.PublishAsync(pending.Destination.Id, pending.Edition, token);
                        await reply(
                            $"Gazette published to #{pending.Destination.Name} (message {messageId}). This approval cannot be reused.",
                            token
                        );
                    }
                }
            }
            else
            {
                await reply(Help, token);
            }
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            session.Pending = null;
            throw;
        }
        catch (Exception exception)
        {
            session.Pending = null;
            logger.LogWarning(
                "Gazette admin command failed with {ExceptionType}; publishing attempted: {Publishing}",
                exception.GetType().Name,
                publishing
            );
            await reply(
                publishing
                        ? "Publication could not be confirmed. The approval has been consumed; check #ai-tavern before requesting another draft. No automatic retry will occur."
                    : drafting
                        ? "I couldn't prepare a Gazette draft; the request failed or timed out. Pending preview was cleared. Try `gazette draft` again."
                    : "The Gazette command failed or timed out; pending approval was cleared. Try `gazette draft` again.",
                cancellationToken
            );
            return true;
        }
        finally
        {
            session.Gate.Release();
        }
    }

    private const string NoDraft = "No live Gazette draft. Request `gazette draft` first; previews expire after 30 minutes or a restart.";

    private async Task DraftAsync(Func<string, CancellationToken, Task> reply, CancellationToken cancellationToken)
    {
        var until = clock.GetUtcNow();
        if (until < session.NextDraftAtUtc)
        {
            await reply("Please wait a minute between draft requests. Your existing preview, if any, is unchanged.", cancellationToken);
            return;
        }
        session.Pending = null;
        var destination = gateway.GetDestination();
        if (destination is null)
        {
            await reply($"I couldn't prepare a draft: {gateway.DestinationError}", cancellationToken);
            return;
        }
        var since = until.AddDays(-7);
        var batch = await gateway.ReadRecentAsync(destination.Id, since, until, cancellationToken);
        if (batch.Sources.Count == 0)
        {
            await reply($"No usable recent human messages were found, so no draft was created. {Coverage(batch)}", cancellationToken);
            return;
        }
        session.NextDraftAtUtc = clock.GetUtcNow().AddMinutes(1);
        var edition = await writer.WriteAsync(batch.Sources, since, until, cancellationToken);
        if (edition.Articles.Count == 0)
        {
            await reply($"No suitable stories were found in this sample; no edition was padded or invented. {Coverage(batch)}", cancellationToken);
            return;
        }
        var text = Render(edition, since, until);
        var sourceUrls = edition.Articles.SelectMany(article => article.SourceUrls).ToHashSet(StringComparer.Ordinal);
        var sources = batch.Sources.Where(source => sourceUrls.Contains(source.Url)).DistinctBy(source => source.Url).ToArray();
        if (sourceUrls.Count != sources.Length || !await gateway.VerifyAsync(destination.Id, sources, cancellationToken))
        {
            await reply("The draft's cited evidence changed or became unavailable. Request a fresh draft.", cancellationToken);
            return;
        }
        var pending = new GazettePendingDraft(Guid.NewGuid().ToString("N")[..12], destination, text, sources, clock.GetUtcNow().AddMinutes(30));
        await reply(Preview(pending) + "\n\n" + Coverage(batch), cancellationToken);
        // Only a successfully delivered private preview becomes approvable.
        cancellationToken.ThrowIfCancellationRequested();
        session.Pending = pending;
    }

    private static string Coverage(GazetteChatBatch batch) =>
        $"Sample: {batch.Sources.Count} human messages, {batch.ChannelsRead}/{batch.AvailableChannels} eligible channels read; {batch.ReadFailures} read failures. "
        + "At most 12 channels, the latest 100 messages per channel, and a bounded text sample; not exhaustive coverage.";

    private static string Preview(GazettePendingDraft pending) =>
        $"PRIVATE DRAFT — not published. Destination: #{pending.Destination.Name}.\n\n{pending.Edition}\n\n"
        + $"Review the stories and source links. Reply `gazette approve {pending.Token}` by {pending.ExpiresAtUtc:yyyy-MM-dd HH:mm} UTC "
        + "to publish exactly the edition above, or `gazette discard`. No edits or regeneration happen during approval.";

    internal static string Render(GazetteEdition edition, DateTimeOffset since, DateTimeOffset until)
    {
        if (edition.Articles.Count is < 1 or > 3)
            throw new InvalidOperationException("Invalid Gazette article count.");
        var text = new StringBuilder($"**THE KHAZAD GAZETTE**\n*Selected dispatches · {since:dd MMM}–{until:dd MMM yyyy} (UTC)*\n");
        foreach (var article in edition.Articles)
        {
            if (string.IsNullOrWhiteSpace(article.Headline) || string.IsNullOrWhiteSpace(article.Body) || article.SourceUrls.Count is < 1 or > 3)
                throw new InvalidOperationException("Invalid Gazette article.");
            text.Append($"\n**{Escape(article.Headline)}**\n{Escape(article.Body)}\n");
            text.AppendJoin(" · ", article.SourceUrls.Select((url, index) => $"[source {index + 1}]({url})"));
            text.Append('\n');
        }
        if (!string.IsNullOrWhiteSpace(edition.Editorial))
            text.Append($"\n**From the editor — fictional satire**\n{Escape(edition.Editorial)}\n");
        if (text.Length > 4000)
            throw new InvalidOperationException("Gazette edition exceeds a single Discord embed.");
        return text.ToString().TrimEnd();
    }

    private static string Escape(string text) => MarkdownRegex().Replace(text, "\\$1");

    [GeneratedRegex(@"([\\`*_~|<>\[\]])")]
    private static partial Regex MarkdownRegex();
}
