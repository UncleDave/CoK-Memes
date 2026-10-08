using System.Globalization;
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
    ILogger<GazetteDirectMessageCommand> logger,
    GazetteIssueService issues,
    IGazettePageRenderer renderer,
    IGazetteIllustrator illustrator,
    IOptions<GazetteOptions> options
)
{
    private string _stage = "handling the command";
    private const string Help =
        "Gazette commands: `gazette draft` — privately draft from up to seven days of recent member-readable chat; "
        + "`gazette show` — repeat your pending preview; `gazette approve <token>` — publish the exact preview to #ai-tavern; "
        + "`gazette discard` — clear it. Approval expires after 30 minutes or a restart. Nothing is posted automatically.";

    public async Task<bool> TryExecuteAsync(
        ulong actorId,
        string content,
        Func<string, CancellationToken, Task> reply,
        CancellationToken cancellationToken,
        Func<GazettePage, CancellationToken, Task>? sendPage = null
    )
    {
        var parts = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (actorId != adminOptions.Value.AdminUserId || parts.Length == 0 || !parts[0].Equals("gazette", StringComparison.OrdinalIgnoreCase))
            return false;

        await session.Gate.WaitAsync(cancellationToken);
        _stage = "handling the command";
        var publishing = false;
        var drafting = parts.Length == 2 && parts[1].Equals("draft", StringComparison.OrdinalIgnoreCase);
        try
        {
            if (session.Pending is { } old && clock.GetUtcNow() >= old.ExpiresAtUtc)
                session.Pending = null;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(6));
            var token = timeout.Token;
            if (drafting)
            {
                await DraftAsync(reply, sendPage, token);
            }
            else if (parts.Length == 2 && parts[1].Equals("discard", StringComparison.OrdinalIgnoreCase))
            {
                session.Pending = null;
                await reply("Gazette draft discarded.", token);
            }
            else if (parts.Length == 2 && parts[1].Equals("show", StringComparison.OrdinalIgnoreCase))
            {
                if (session.Pending is { } pending)
                    await SendPreviewAsync(pending, reply, sendPage, token);
                else
                    await reply(NoDraft, token);
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
                    _stage = "checking publication evidence";
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
                    else if (!await ReserveIssueAsync(pending, token))
                    {
                        await reply(
                            "This issue number has already been reserved by another publication. Request a fresh draft; this preview was not sent.",
                            token
                        );
                    }
                    else
                    {
                        publishing = true;
                        _stage = "sending the publication";
                        var messageId = await gateway.PublishAsync(
                            pending.Destination.Id,
                            pending.Edition,
                            pending.PrintEdition,
                            pending.Token,
                            token
                        );
                        _stage = "saving publication acknowledgement";
                        await issues.MarkPublishedAsync(pending.IssueNumber, pending.Token, messageId, token);
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
            var validation = exception as GazetteDraftValidationException;
            logger.LogWarning(
                "Gazette admin command failed at {Stage} with {ExceptionType}; reason {Reason}; field {Field}; actual length {ActualLength}; limit {Limit}; failure site {FailureSite}; publishing attempted: {Publishing}",
                _stage,
                exception.GetType().Name,
                validation?.Failure.ToString() ?? "UnexpectedFailure",
                validation?.Field.ToString() ?? "none",
                validation?.ActualLength,
                validation?.Limit,
                exception.TargetSite is { } method ? $"{method.DeclaringType?.FullName}.{method.Name}" : "unknown",
                publishing
            );
            await reply(
                publishing
                        ? "Publication could not be confirmed. The approval has been consumed; check #ai-tavern before requesting another draft. No automatic retry will occur."
                    : drafting
                        ? $"I couldn't prepare a Gazette draft while {_stage}. {DescribeValidation(validation)}Pending preview was cleared. Try `gazette draft` again."
                    : $"The Gazette command failed while {_stage}; pending approval was cleared. Try `gazette draft` again.",
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

    private async Task<bool> ReserveIssueAsync(GazettePendingDraft pending, CancellationToken cancellationToken)
    {
        _stage = "reserving the issue number";
        return await issues.TryReserveAsync(pending.IssueNumber, pending.Token, pending.Destination.Id, cancellationToken);
    }

    private static string DescribeValidation(GazetteDraftValidationException? exception) =>
        exception switch
        {
            { Failure: GazetteValidationFailure.IncompleteResponse } => "The AI response was cut short at its output limit. ",
            { Failure: GazetteValidationFailure.FieldTooLong } error =>
                $"The AI exceeded the {error.Field} limit ({error.ActualLength}/{error.Limit} characters). ",
            { Failure: GazetteValidationFailure.InvalidJson } => "The AI returned incomplete or invalid JSON. ",
            { } error => $"The AI response failed validation ({error.Failure}/{error.Field}). ",
            _ => "",
        };

    private async Task DraftAsync(
        Func<string, CancellationToken, Task> reply,
        Func<GazettePage, CancellationToken, Task>? sendPage,
        CancellationToken cancellationToken
    )
    {
        var until = clock.GetUtcNow();
        if (until < session.NextDraftAtUtc)
        {
            await reply("Please wait a minute between draft requests. Your existing preview, if any, is unchanged.", cancellationToken);
            return;
        }
        session.Pending = null;
        if (session.Illustration is { } expired && clock.GetUtcNow() >= expired.ExpiresAtUtc)
            session.Illustration = null;
        _stage = "resolving the newspaper audience";
        var destination = gateway.GetDestination();
        if (destination is null)
        {
            await reply($"I couldn't prepare a draft: {gateway.DestinationError}", cancellationToken);
            return;
        }
        var since = until.AddDays(-7);
        _stage = "sending draft progress";
        await reply("Preparing a private Gazette draft. An optional illustration may take a minute.", cancellationToken);
        _stage = "reading recent chat and server names";
        var batch = await gateway.ReadRecentAsync(destination.Id, since, until, cancellationToken);
        if (batch.Sources.Count == 0)
        {
            await reply($"No usable recent human messages were found, so no draft was created. {Coverage(batch)}", cancellationToken);
            return;
        }
        session.NextDraftAtUtc = clock.GetUtcNow().AddMinutes(1);
        _stage = "writing stories";
        var edition = await writer.WriteAsync(batch.Sources, since, until, cancellationToken);
        if (edition.Articles.Count == 0)
        {
            await reply($"No suitable stories were found in this sample; no edition was padded or invented. {Coverage(batch)}", cancellationToken);
            return;
        }
        _stage = "reading the next issue number";
        var issueNumber = await issues.GetNextAsync(cancellationToken);
        _stage = "formatting the readable edition";
        var text = Render(edition, since, until, issueNumber);
        var sourceUrls = edition.Articles.SelectMany(article => article.SourceUrls).ToHashSet(StringComparer.Ordinal);
        var sources = batch.Sources.Where(source => sourceUrls.Contains(source.Url)).DistinctBy(source => source.Url).ToArray();
        _stage = "checking draft evidence";
        if (sourceUrls.Count != sources.Length || !await gateway.VerifyAsync(destination.Id, sources, cancellationToken))
        {
            await reply("The draft's cited evidence changed or became unavailable. Request a fresh draft.", cancellationToken);
            return;
        }
        byte[]? artwork = null;
        var leadUrls = edition.Articles[0].SourceUrls.ToHashSet(StringComparer.Ordinal);
        var evidenceKey = GazetteCachedIllustration.CreateEvidenceKey(sources.Where(source => leadUrls.Contains(source.Url)).ToArray());
        var artworkStatus = "Illustration: omitted—the writer did not propose a visual gag for this lead.";
        if (!options.Value.IllustrationsEnabled)
            artworkStatus = "Illustration: disabled in Gazette configuration.";
        else if (edition.IllustrationPrompt is { } concept)
        {
            try
            {
                if (session.Illustration is { } cached && cached.ExpiresAtUtc > clock.GetUtcNow() && cached.EvidenceKey == evidenceKey)
                {
                    artwork = cached.Image;
                    artworkStatus = "Illustration: reused from the same verified lead-story evidence; no new image request.";
                }
                else
                {
                    _stage = "generating the lead illustration";
                    using var artTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    artTimeout.CancelAfter(TimeSpan.FromMinutes(2));
                    artwork = await illustrator.GenerateAsync(concept, artTimeout.Token).WaitAsync(artTimeout.Token);
                    artworkStatus = "Illustration: newly generated for this lead.";
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var failure = GazetteImageFailure.FromException(exception);
                logger.LogWarning(
                    "Gazette illustration unavailable at {Stage} with {ExceptionType}; HTTP status {HttpStatus}; code {Code}; parameter {Parameter}; timed out {TimedOut}",
                    _stage,
                    exception.GetType().Name,
                    failure.HttpStatus,
                    failure.Code,
                    failure.Parameter,
                    failure.TimedOut
                );
                artworkStatus = $"Illustration was unavailable: {failure.Description}; this edition has no image.";
            }
        }
        GazettePrintEdition printEdition;
        _stage = "rendering newspaper pages";
        try
        {
            printEdition = renderer.Render(edition, issueNumber, FormatDates(since, until), artwork);
        }
        catch when (artwork is not null)
        {
            _stage = "rendering newspaper pages without artwork";
            printEdition = renderer.Render(edition, issueNumber, FormatDates(since, until), null);
            artwork = null;
            if (session.Illustration?.EvidenceKey == evidenceKey)
                session.Illustration = null;
            artworkStatus = "Illustration: image could not be rendered; this edition has no image.";
        }
        // Name/source verification happens again after optional image generation, before preview delivery.
        _stage = "rechecking evidence after rendering";
        if (!await gateway.VerifyAsync(destination.Id, sources, cancellationToken))
        {
            await reply("The draft's cited evidence changed while the page was being prepared. Request a fresh draft.", cancellationToken);
            return;
        }
        if (artwork is not null && !ReferenceEquals(session.Illustration?.Image, artwork))
            session.Illustration = new(evidenceKey, artwork, clock.GetUtcNow().AddMinutes(30));
        var pending = new GazettePendingDraft(
            Guid.NewGuid().ToString("N")[..12],
            destination,
            text,
            sources,
            clock.GetUtcNow().AddMinutes(30),
            issueNumber,
            printEdition
        )
        {
            IllustrationStatus = artworkStatus,
        };
        await SendPreviewAsync(pending, reply, sendPage, cancellationToken);
        _stage = "sending private sampling diagnostics";
        var slimEdition = edition.Articles.Count == 1 ? " Only one supported story was selected; this is a slim, single-page edition." : "";
        await reply(Coverage(batch) + slimEdition, cancellationToken);
        // Only a successfully delivered private preview becomes approvable.
        cancellationToken.ThrowIfCancellationRequested();
        session.Pending = pending;
    }

    private static string Coverage(GazetteChatBatch batch) =>
        $"Sample: {batch.Sources.Count} human messages, {batch.ChannelsRead}/{batch.AvailableChannels} eligible channels read; {batch.ReadFailures} read failures. "
        + "At most 12 channels, the latest 100 messages per channel, and a bounded text sample; not exhaustive coverage.";

    private string Preview(GazettePendingDraft pending) =>
        $"PRIVATE DRAFT — not published. Destination: #{pending.Destination.Name}. "
        + $"The guild post will show only the {pending.PrintEdition.Pages.Count} newspaper {(pending.PrintEdition.Pages.Count == 1 ? "page" : "pages")} and a Read text & sources button; this text opens privately on click.\n"
        + $"{pending.IllustrationStatus}\n\n{pending.Edition}\n\n"
        + $"Review all pages, stories and source links. Reply `gazette approve {pending.Token}` within {Math.Max(0, (int)Math.Ceiling((pending.ExpiresAtUtc - clock.GetUtcNow()).TotalMinutes))} minutes "
        + "to publish exactly the edition above, or `gazette discard`. No edits or regeneration happen during approval.";

    private async Task SendPreviewAsync(
        GazettePendingDraft pending,
        Func<string, CancellationToken, Task> reply,
        Func<GazettePage, CancellationToken, Task>? sendPage,
        CancellationToken cancellationToken
    )
    {
        if (sendPage is null)
            throw new InvalidOperationException("Private newspaper page delivery is unavailable.");
        for (var index = 0; index < pending.PrintEdition.Pages.Count; index++)
        {
            _stage = $"sending private newspaper page {index + 1}";
            await sendPage(pending.PrintEdition.Pages[index], cancellationToken);
        }
        _stage = "sending private preview text";
        await reply(Preview(pending), cancellationToken);
    }

    internal static string FormatDates(DateTimeOffset since, DateTimeOffset until)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
        var start = TimeZoneInfo.ConvertTime(since, zone);
        var end = TimeZoneInfo.ConvertTime(until, zone);
        return start.Year == end.Year && start.Month == end.Month
            ? $"{start.Day}–{end.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}"
            : $"{start.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}–{end.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}";
    }

    internal static string Render(GazetteEdition edition, DateTimeOffset since, DateTimeOffset until, long issueNumber = 1)
    {
        if (edition.Articles.Count is < 1 or > 3)
            throw new InvalidOperationException("Invalid Gazette article count.");
        var text = new StringBuilder($"**THE KHAZAD GAZETTE**\n*Issue No. {issueNumber} · {FormatDates(since, until)}*\n");
        foreach (var article in edition.Articles)
        {
            if (string.IsNullOrWhiteSpace(article.Headline) || string.IsNullOrWhiteSpace(article.Body) || article.SourceUrls.Count is < 1 or > 3)
                throw new InvalidOperationException("Invalid Gazette article.");
            text.Append($"\n**{Escape(article.Headline)}**\n{Escape(article.Body)}\n");
            text.AppendJoin(" · ", article.SourceUrls.Select((url, index) => $"[source {index + 1}]({url})"));
            text.Append('\n');
        }
        if (!string.IsNullOrWhiteSpace(edition.Editorial))
            text.Append($"\n**Classifieds**\n{Escape(edition.Editorial)}\n");
        if (text.Length > 4000)
            throw new InvalidOperationException("Gazette edition exceeds a single Discord embed.");
        return text.ToString().TrimEnd();
    }

    private static string Escape(string text) => MarkdownRegex().Replace(text, "\\$1");

    [GeneratedRegex(@"([\\`*_~|<>\[\]])")]
    private static partial Regex MarkdownRegex();
}
