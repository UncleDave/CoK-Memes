using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public sealed partial class SpecialsDirectMessageCommand(
    ISpecialsGateway gateway,
    ISpecialsWriter writer,
    SpecialsSession session,
    IOptions<DirectMessageHandlerOptions> adminOptions,
    TimeProvider clock,
    ILogger<SpecialsDirectMessageCommand> logger,
    ISpecialsLoreReader lore
)
{
    private const string NoDraft = "No live specials draft. Request `specials draft` first.";
    private const string Help =
        "Specials commands: `specials draft` — privately draft this week's menu; `specials show` — review it; "
        + "`specials approve <token>` — publish to the configured bot channel; `specials discard` — clear it. Approval expires after 30 minutes or a restart.";

    public async Task<bool> TryExecuteAsync(
        ulong actorId,
        string content,
        Func<string, Embed?, CancellationToken, Task> sendReply,
        CancellationToken cancellationToken
    )
    {
        Task reply(string text, CancellationToken token) => sendReply(text, null, token);

        var parts = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (actorId != adminOptions.Value.AdminUserId || parts.Length == 0 || !parts[0].Equals("specials", StringComparison.OrdinalIgnoreCase))
            return false;
        await session.Gate.WaitAsync(cancellationToken);
        var stage = "handling the command";
        var publishing = false;
        try
        {
            if (session.Pending is { } old && clock.GetUtcNow() >= old.ExpiresAtUtc)
                session.Pending = null;
            if (parts.Length == 2 && parts[1].Equals("draft", StringComparison.OrdinalIgnoreCase))
            {
                session.Pending = null;
                stage = "resolving the publication audience";
                var destination = gateway.GetDestination();
                if (destination is null)
                {
                    await reply($"Cannot prepare specials: {gateway.DestinationError}", cancellationToken);
                    return true;
                }
                var until = clock.GetUtcNow();
                var since = until.AddDays(-7);
                await reply("Reading this week's chat and looking for menu ingredients. This may take a few minutes.", cancellationToken);
                stage = "reading weekly chat";
                var batch = await gateway.ReadWeekAsync(destination.Id, since, until, cancellationToken);
                var plan = SpecialsDiscoveryPlan.Create(batch.Sources, since, until);
                var coverage = Coverage(batch, plan);
                var candidates = new List<SpecialsEvent>();
                for (var index = 0; index < plan.Batches.Count; index++)
                {
                    stage = $"discovering weekly events (batch {index + 1}/{plan.Batches.Count})";
                    candidates.AddRange(await writer.DiscoverAsync(plan.Batches[index], until, cancellationToken));
                }
                stage = "grouping and selecting events";
                // Large candidate sets are reduced in bounded groups before the final whole-week comparison.
                IReadOnlyList<SpecialsEvent> selected = candidates;
                while (JsonSerializer.Serialize(new { candidates = selected }).Length > 90000)
                {
                    var reduced = new List<SpecialsEvent>();
                    foreach (var group in selected.Chunk(20))
                        reduced.AddRange(await writer.SelectAsync(group, until, cancellationToken));
                    if (reduced.Count >= selected.Count)
                        throw new SpecialsValidationException("SelectionBounds");
                    selected = reduced;
                }
                selected = await writer.SelectAsync(selected, until, cancellationToken);
                stage = "retrieving event context and server names";
                var contexts = SpecialsDiscoveryPlan.AddContext(selected, batch.Sources);
                var rawContext = contexts.SelectMany(item => item.Sources).DistinctBy(source => source.Url).ToArray();
                var resolved = rawContext.Length == 0 ? [] : await gateway.ResolveNamesAsync(rawContext, cancellationToken);
                var names = resolved.ToDictionary(source => source.Url, StringComparer.Ordinal);
                contexts = contexts.Select(item => item with { Sources = item.Sources.Select(source => names[source.Url]).ToArray() }).ToArray();
                stage = "reading established guild lore";
                var loreBatch = await lore.ReadAsync(contexts, cancellationToken);
                contexts = loreBatch.Contexts;
                stage = "writing the specials menu";
                var dishes = await writer.WriteAsync(contexts, until, cancellationToken);
                if (dishes.Count == 0)
                {
                    await reply($"No suitable moments found for a menu.\n{coverage}", cancellationToken);
                    return true;
                }
                stage = "formatting the private preview";
                var menu = Render(dishes, since, until);
                var urls = dishes.SelectMany(dish => dish.SourceUrls).ToHashSet(StringComparer.Ordinal);
                var evidence = resolved.Where(source => urls.Contains(source.Url)).ToArray();
                var suppliedLore = contexts.SelectMany(item => item.Lore).DistinctBy(entry => entry.Key).ToArray();
                var usedKeys = dishes.SelectMany(dish => dish.LoreKeys).ToHashSet(StringComparer.Ordinal);
                var usedLore = suppliedLore.Where(entry => usedKeys.Contains(entry.Key)).ToArray();
                stage = "checking draft evidence";
                if (
                    urls.Count != evidence.Length
                    || usedKeys.Count != usedLore.Length
                    || !await gateway.VerifyAsync(destination.Id, evidence, cancellationToken)
                    || !await lore.VerifyAsync(usedLore, cancellationToken)
                )
                {
                    await reply("The selected evidence or lore changed or became unavailable. Request `specials draft` again.", cancellationToken);
                    return true;
                }
                var pending = new SpecialsPendingDraft(
                    Guid.NewGuid().ToString("N")[..12],
                    destination,
                    menu,
                    RenderEvidence(dishes, usedLore),
                    coverage
                        + $" Lore: {suppliedLore.Length} related entries supplied, {usedLore.Length} used; {loreBatch.OmittedEntries} omitted by bounds.",
                    evidence,
                    clock.GetUtcNow().AddMinutes(30)
                )
                {
                    Lore = usedLore,
                };
                stage = "sending the private preview";
                await PreviewAsync(pending, sendReply, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                session.Pending = pending;
            }
            else if (parts.Length == 2 && parts[1].Equals("show", StringComparison.OrdinalIgnoreCase))
            {
                if (session.Pending is { } pending)
                    await PreviewAsync(pending, sendReply, cancellationToken);
                else
                    await reply(NoDraft, cancellationToken);
            }
            else if (parts.Length == 2 && parts[1].Equals("discard", StringComparison.OrdinalIgnoreCase))
            {
                session.Pending = null;
                await reply("Specials draft discarded.", cancellationToken);
            }
            else if (parts.Length == 3 && parts[1].Equals("approve", StringComparison.OrdinalIgnoreCase))
            {
                var pending = session.Pending;
                if (pending is null)
                    await reply(NoDraft, cancellationToken);
                else if (parts[2] != pending.Token)
                    await reply("Use the exact `specials approve <token>` from your preview.", cancellationToken);
                else
                {
                    session.Pending = null;
                    stage = "checking publication evidence";
                    if (
                        gateway.GetDestination()?.Id != pending.Destination.Id
                        || !await gateway.VerifyAsync(pending.Destination.Id, pending.Sources, cancellationToken)
                        || !await lore.VerifyAsync(pending.Lore, cancellationToken)
                        || clock.GetUtcNow() >= pending.ExpiresAtUtc
                    )
                        await reply(
                            "The destination, evidence or lore changed, became unavailable, or approval expired. Request `specials draft` again.",
                            cancellationToken
                        );
                    else if (gateway.GetPublicationError(pending.Destination.Id) is { } error)
                        await reply($"Cannot publish specials: {error} Request a fresh draft after fixing posting permissions.", cancellationToken);
                    else
                    {
                        stage = "sending the menu";
                        publishing = true;
                        var id = await gateway.PublishAsync(pending.Destination.Id, pending.Menu, cancellationToken);
                        await reply($"Specials published to #{pending.Destination.Name} (message {id}).", cancellationToken);
                    }
                }
            }
            else
                await reply(Help, cancellationToken);
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
            var validation = exception as SpecialsValidationException;
            var category = validation?.Category ?? "UnexpectedFailure";
            logger.LogWarning(
                "Specials failed while {Stage}; type {ExceptionType}; category {Category}; field {Field}; actual length {ActualLength}; limit {Limit}; publication attempted {Publishing}",
                stage,
                exception.GetType().Name,
                category,
                validation?.Field,
                validation?.ActualLength,
                validation?.Limit,
                publishing
            );
            await reply(
                publishing
                    ? "Publication could not be confirmed. Approval was consumed; check the destination channel before requesting another draft."
                    : $"Could not prepare specials while {stage}. {DescribeFailure(validation)}Pending approval was cleared; try `specials draft` again.",
                cancellationToken
            );
            return true;
        }
        finally
        {
            session.Gate.Release();
        }
    }

    private static string DescribeFailure(SpecialsValidationException? exception) =>
        exception switch
        {
            { Category: "TextTooLong", Field: { } field, ActualLength: { } length, Limit: { } limit } =>
                $"The AI exceeded the {field} limit ({length}/{limit} characters). ",
            { Category: "MultilineText", Field: { } field } => $"The AI returned multiple lines for {field}; one line is required. ",
            { Category: "MissingText", Field: { } field } => $"The AI returned an empty {field}. ",
            { Category: "IncompleteResponse" } => "The AI response was cut short at its output limit. ",
            { } failure => $"The AI response failed validation ({failure.Category}). ",
            _ => "",
        };

    private async Task PreviewAsync(
        SpecialsPendingDraft pending,
        Func<string, Embed?, CancellationToken, Task> reply,
        CancellationToken cancellationToken
    )
    {
        await reply($"PRIVATE SPECIALS DRAFT — #{pending.Destination.Name}", pending.Menu, cancellationToken);
        await reply(
            $"**Selected moments & sources**\n{pending.Evidence}{RenderLore(pending.Lore)}\n\n{pending.Coverage}\n\n"
                + (pending.Lore.Count == 0 ? "Review the menu and sources. " : "Review the menu, sources and lore callbacks. ")
                + $"Reply `specials approve {pending.Token}` within {Math.Max(0, (int)Math.Ceiling((pending.ExpiresAtUtc - clock.GetUtcNow()).TotalMinutes))} minutes to publish, or `specials discard`.",
            null,
            cancellationToken
        );
    }

    private static string Coverage(SpecialsChatBatch batch, SpecialsDiscoveryPlan plan) =>
        $"Coverage: {batch.Sources.Count} usable human messages; {batch.CompleteChannels}/{batch.EligibleChannels} eligible channels fully read; "
        + $"{batch.FailedChannels} failed, {batch.LimitedChannels} limited; {batch.HistoryReads} history reads; {batch.ExcludedMessages} records excluded. "
        + $"Discovery: {plan.Batches.Count} batches; {plan.UnassessedMessages} messages not assessed."
        + (
            batch.FailedChannels + batch.LimitedChannels + plan.UnassessedMessages > 0
                ? " Weekly coverage is incomplete; important events may be missing."
                : ""
        );

    internal static Embed Render(IReadOnlyList<SpecialsDish> dishes, DateTimeOffset since, DateTimeOffset until)
    {
        if (dishes.Count is < 1 or > 5 || dishes.Select(dish => dish.EventIndex).Distinct().Count() != dishes.Count)
            throw new SpecialsValidationException("MenuCount");
        var menu = new EmbedBuilder()
            .WithTitle("🍟 Crossroads Mega Chippy")
            .WithDescription($"**Specials of the week**\n{FormatDates(since, until)}")
            .WithColor(Color.Gold);
        foreach (var dish in dishes)
        {
            var name = $"{Escape(dish.Name)} — {Escape(dish.Price)}";
            var description = Escape(dish.Description);
            if (name.Length > 256 || description.Length > 1024)
                throw new SpecialsValidationException("MenuLength");
            menu.AddField(name, description, inline: false);
        }
        if (menu.Length > 6000)
            throw new SpecialsValidationException("MenuLength");
        return menu.Build();
    }

    private static string RenderEvidence(IReadOnlyList<SpecialsDish> dishes, IReadOnlyList<SpecialsLoreReference> lore) =>
        string.Join(
            "\n\n",
            dishes.Select(
                (dish, index) =>
                    $"{index + 1}. **{Escape(dish.Name)}** — {Escape(dish.EventSummary)}\n"
                    + string.Join(" · ", dish.SourceUrls.Select((url, number) => $"[source {number + 1}]({url})"))
                    + (
                        dish.LoreKeys.Count == 0
                            ? ""
                            : "\nCallback: " + string.Join(", ", dish.LoreKeys.Select(key => Escape(lore.Single(entry => entry.Key == key).Name)))
                    )
            )
        );

    private static string Escape(string text) => MarkdownRegex().Replace(text, "\\$1");

    private static string RenderLore(IReadOnlyList<SpecialsLoreReference> references) =>
        references.Count == 0
            ? ""
            : "\n\n**Established lore used**\n"
                + string.Join("\n\n", references.Select(entry => $"**{Escape(entry.Name)}**\n{Escape(entry.Content)}"));

    internal static string FormatDates(DateTimeOffset since, DateTimeOffset until)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
        var start = TimeZoneInfo.ConvertTime(since, zone);
        var end = TimeZoneInfo.ConvertTime(until, zone);
        return start.Year == end.Year && start.Month == end.Month
            ? $"{start.Day}–{end.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}"
            : $"{start.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}–{end.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}";
    }

    [GeneratedRegex(@"([\\`*_~|<>\[\]])")]
    private static partial Regex MarkdownRegex();
}
