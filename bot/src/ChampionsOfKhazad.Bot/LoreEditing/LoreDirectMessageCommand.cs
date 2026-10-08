using System.Text.Json;
using ChampionsOfKhazad.Bot.GenAi;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class LoreDirectMessageCommand(
    IEditLoreStore store,
    ILoreEditPlanner planner,
    LoreEditorSession session,
    IOptions<DirectMessageHandlerOptions> options,
    TimeProvider clock,
    ILogger<LoreDirectMessageCommand> logger
)
{
    private const string Help =
        "DM an explicit lore instruction, e.g. ‘Add Grim, a resto shaman who keeps dying to elevators’ or ‘Update Grim: his main is now a paladin; keep the elevator joke’. "
        + "Clear single-entry additions/corrections save directly. Deletions and broad rewrites need `lore confirm <token>`. "
        + "Commands: `lore list [page]`, `lore show <name>`, `lore history <name>`, `lore undo <name>` (or `undo` for your last edit), `lore cancel`, `lore reset`. "
        + "Unknown member details stay Unknown. Paste or explain linked messages; links are not fetched. Conversation expires after 30 minutes or a restart.";

    public async Task<string?> ExecuteAsync(ulong actorId, string content, CancellationToken cancellationToken)
    {
        if (actorId != options.Value.AdminUserId)
            return null;
        var instruction = content.Trim();
        if (instruction.Length == 0)
            return Help;
        if (instruction.Length > 8000)
            return "Please split this into smaller, single-entry lore requests (at most 8,000 characters). Nothing was changed.";

        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            if (clock.GetUtcNow() - session.LastUsedAtUtc >= TimeSpan.FromMinutes(30))
                session.Reset();
            session.LastUsedAtUtc = clock.GetUtcNow();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            var reply = await ExecuteCommandAsync(actorId, instruction, timeout.Token);
            session.Turns.Add(new(instruction, reply.Length <= 4000 ? reply : reply[..4000] + " [response truncated in conversation context]"));
            while (session.Turns.Count > 8)
                session.Turns.RemoveAt(0);
            return reply;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            session.Turns.Clear();
            session.Pending = null;
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Admin DM lore command failed");
            session.Turns.Clear();
            session.Pending = null;
            return "The lore command failed or timed out; its effect could not be confirmed. Check `lore show <name>` and `lore history <name>` before retrying. No success is assumed.";
        }
        finally
        {
            session.Gate.Release();
        }
    }

    private async Task<string> ExecuteCommandAsync(ulong actorId, string instruction, CancellationToken cancellationToken)
    {
        var parts = instruction.Split((char[]?)null, 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts[0].Equals("lore", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length == 1)
                return Help;
            var command = parts[1].ToLowerInvariant();
            var argument = parts.Length == 3 ? parts[2].Trim() : "";
            switch (command)
            {
                case "help":
                    return Help;
                case "cancel":
                case "reset":
                    session.Reset();
                    return "Lore conversation and pending confirmation cleared. Saved lore is unchanged.";
                case "confirm":
                    return await ConfirmAsync(actorId, argument, cancellationToken);
                case "undo":
                    return await UndoAsync(actorId, argument, cancellationToken);
                case "show":
                case "history":
                    return argument.Length == 0 ? Help : await InspectAsync(argument, command == "history", cancellationToken);
                case "list":
                    if (argument.Length > 0 && (!int.TryParse(argument, out var page) || page is < 1 or > 100))
                        return Help;
                    var entries = await store.GetEntriesAsync(cancellationToken);
                    var pageNumber = argument.Length == 0 ? 1 : int.Parse(argument);
                    return $"Lore — page {pageNumber}/{Math.Max(1, (entries.Count + 19) / 20)}\n"
                        + string.Join(
                            '\n',
                            entries.OrderBy(entry => entry.Name).Skip((pageNumber - 1) * 20).Take(20).Select(entry => Display(entry.Name))
                        );
            }
            instruction = instruction[4..].Trim();
        }
        if (instruction.Equals("undo", StringComparison.OrdinalIgnoreCase))
            return await UndoAsync(actorId, "", cancellationToken);

        // Any new natural-language request replaces an earlier, unconfirmed proposal.
        session.Pending = null;
        var catalog = await store.GetEntriesAsync(cancellationToken);
        var plan = await planner.PlanAsync(instruction, catalog.Select(entry => entry.Entry!).ToArray(), session.Turns, cancellationToken);
        if (plan.Action is "none" or "clarify")
            return plan.Reply;
        if (plan.Action is not ("create" or "update" or "delete") || plan.Name is null || plan.Kind is not ("member" or "guild"))
            throw new InvalidOperationException("Invalid lore plan.");

        LoreEditState expected;
        if (plan.Action == "create")
        {
            if (
                catalog.Any(entry =>
                    entry.Name.Equals(plan.Name, StringComparison.OrdinalIgnoreCase)
                    || entry.Entry!.Aliases.Contains(plan.Name, StringComparer.OrdinalIgnoreCase)
                )
            )
                return "That name or alias already belongs to existing lore. Please request an update instead. Nothing was changed.";
            expected = await store.GetEntryAsync(plan.Name, cancellationToken);
            if (expected.Entry is not null)
                return "That entry already exists. Please request an update instead. Nothing was changed.";
        }
        else
        {
            var supplied = catalog.SingleOrDefault(entry => entry.Name.Equals(plan.Name, StringComparison.Ordinal));
            if (supplied is null || supplied.Entry!.Kind != plan.Kind)
                return "I couldn't resolve that to one exact existing entry. Please give its name from `lore list`. Nothing was changed.";
            expected = await store.GetEntryAsync(supplied.Name, cancellationToken);
            if (expected.Revision != supplied.Revision || !Same(expected.Entry, supplied.Entry))
                return Conflict;
        }
        var after = plan.Action == "delete" ? null : Apply(expected.Entry ?? new(expected.Name, plan.Kind), plan.Changes);
        if (Same(expected.Entry, after))
            return $"No changes needed for {Display(expected.Name)}.";
        if (after?.Kind == "guild" && string.IsNullOrWhiteSpace(after.Content))
            throw new InvalidOperationException("Guild lore cannot be blank.");
        if (plan.Action == "delete" || plan.RequiresConfirmation || BroadRewrite(expected.Entry, after))
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            session.Pending = new(token, expected, after, plan.Action, clock.GetUtcNow().AddMinutes(10));
            return $"Not saved yet. Proposed {plan.Action} for {Display(expected.Name)}:\n{Changes(expected.Entry, after)}\n"
                + $"Reply `lore confirm {token}` within 10 minutes, or `lore cancel`.";
        }
        return await SaveAsync(actorId, expected, after, plan.Action, cancellationToken);
    }

    private const string Conflict =
        "That lore changed while this edit was being prepared. Nothing was overwritten. Please inspect it and send a fresh request.";

    private async Task<string> ConfirmAsync(ulong actorId, string token, CancellationToken cancellationToken)
    {
        var pending = session.Pending;
        if (pending is null || clock.GetUtcNow() >= pending.ExpiresAtUtc)
        {
            session.Pending = null;
            return "No live pending confirmation. Please send a fresh lore request.";
        }
        if (!pending.Token.Equals(token, StringComparison.Ordinal))
            return "Use the exact `lore confirm <token>` from the preview, or `lore cancel`. Nothing was changed.";
        session.Pending = null;
        return await SaveAsync(actorId, pending.Expected, pending.After, pending.Action, cancellationToken);
    }

    private async Task<string> UndoAsync(ulong actorId, string name, CancellationToken cancellationToken)
    {
        var lastEdit = name.Length == 0;
        name = lastEdit ? session.LastEditedName ?? "" : name;
        if (name.Length == 0)
            return "Give the entry name: `lore undo <name>`.";
        session.Pending = null;
        var state = await store.GetEntryAsync(name, cancellationToken);
        if (lastEdit && state.Revision != session.LastEditedRevision)
            return "Your last edited entry has changed since then. Inspect its history before using `lore undo <name>`. Nothing was changed.";
        var latest = state.History.LastOrDefault();
        if (latest is null || latest.Id != state.Revision)
            return "No recoverable revision was found for that entry.";
        return await SaveAsync(actorId, state, latest.Before, "undo", cancellationToken);
    }

    private async Task<string> SaveAsync(
        ulong actorId,
        LoreEditState expected,
        LoreEntrySnapshot? after,
        string action,
        CancellationToken cancellationToken
    )
    {
        var revision = new LoreRevision(
            Guid.NewGuid().ToString("N"),
            clock.GetUtcNow().UtcDateTime,
            "admin-dm",
            actorId,
            action,
            expected.Entry,
            after
        );
        if (!await store.TrySaveAsync(expected, revision, cancellationToken))
            return Conflict;
        session.LastEditedName = expected.Name;
        session.LastEditedRevision = revision.Id;
        return $"Saved {Display(expected.Name)}{(action == "undo" ? " (undo)" : "")}.\n{Changes(expected.Entry, after, compact: true)}\n"
            + $"Use `lore show {Display(expected.Name)}` to inspect, or `undo` to reverse this change.";
    }

    private async Task<string> InspectAsync(string name, bool history, CancellationToken cancellationToken)
    {
        var state = await store.GetEntryAsync(name, cancellationToken);
        if (!state.Exists)
            return "No lore entry with that name was found.";
        if (!history)
            return state.Entry is null ? "That entry is deleted; its revisions can still be inspected or undone." : Describe(state.Entry);
        return $"Recent revisions for {Display(state.Name)} (newest first; at most 20 retained):\n"
            + (
                state.History.Count == 0
                    ? "No revisions recorded yet."
                    : string.Join(
                        '\n',
                        state
                            .History.Reverse()
                            .Select(revision =>
                                $"{revision.CreatedAtUtc:u} — {revision.Action}, {revision.Source}\n{Changes(revision.Before, revision.After, compact: true)}"
                            )
                    )
            )
            + $"\n`lore undo {Display(state.Name)}` reverses the latest revision only (including an undo).";
    }

    private static LoreEntrySnapshot Apply(LoreEntrySnapshot entry, LoreEditChanges changes) =>
        entry with
        {
            Content = changes.Content ?? entry.Content,
            Pronouns = changes.Pronouns ?? entry.Pronouns,
            Nationality = changes.Nationality ?? entry.Nationality,
            MainCharacter = changes.MainCharacter ?? entry.MainCharacter,
            Biography =
                changes.Biography is null ? entry.Biography
                : changes.Biography.Length == 0 ? null
                : changes.Biography,
            Aliases = changes.Aliases ?? entry.Aliases,
            Roles = changes.Roles ?? entry.Roles,
        };

    private static bool Same(LoreEntrySnapshot? first, LoreEntrySnapshot? second) =>
        JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second);

    private static bool BroadRewrite(LoreEntrySnapshot? before, LoreEntrySnapshot? after)
    {
        if (before is null || after is null)
            return false;
        var original = before.Kind == "guild" ? before.Content : before.Biography ?? "";
        var updated = after.Kind == "guild" ? after.Content : after.Biography ?? "";
        return (original.Length > 200 && updated.Length < original.Length * 0.75) || ChangedFields(before, after).Count() >= 4;
    }

    private static IEnumerable<(string Name, string Before, string After)> ChangedFields(LoreEntrySnapshot before, LoreEntrySnapshot after)
    {
        var fields =
            before.Kind == "guild"
                ? new[] { ("Content", before.Content, after.Content) }
                : new[]
                {
                    ("Pronouns", before.Pronouns, after.Pronouns),
                    ("Nationality", before.Nationality, after.Nationality),
                    ("Main character", before.MainCharacter, after.MainCharacter),
                    ("Biography", before.Biography ?? "", after.Biography ?? ""),
                    ("Aliases", string.Join(", ", before.Aliases), string.Join(", ", after.Aliases)),
                    ("Roles", string.Join(", ", before.Roles), string.Join(", ", after.Roles)),
                };
        return fields.Where(field => field.Item2 != field.Item3);
    }

    private static string Changes(LoreEntrySnapshot? before, LoreEntrySnapshot? after, bool compact = false)
    {
        if (after is null)
            return before is null ? "Entry remains deleted." : $"Delete entry. Previous lore:\n{Describe(before, compact)}";
        if (before is null)
            return $"Create entry:\n{Describe(after, compact)}";
        return string.Join('\n', ChangedFields(before, after).Select(field => FieldChange(field.Name, field.Before, field.After, compact)));
    }

    private static string FieldChange(string name, string before, string after, bool compact)
    {
        if (!compact || Math.Max(before.Length, after.Length) <= 160)
            return $"{name}: {Display(before)} → {Display(after)}";

        var prefix = 0;
        while (prefix < Math.Min(before.Length, after.Length) && before[prefix] == after[prefix])
            prefix++;
        if (InsideSurrogatePair(before, prefix) || InsideSurrogatePair(after, prefix))
            prefix--;

        var suffix = 0;
        while (suffix < Math.Min(before.Length, after.Length) - prefix && before[^(suffix + 1)] == after[^(suffix + 1)])
            suffix++;
        if (InsideSurrogatePair(before, before.Length - suffix) || InsideSurrogatePair(after, after.Length - suffix))
            suffix--;

        var removed = before[prefix..(before.Length - suffix)];
        var added = after[prefix..(after.Length - suffix)];
        if (removed.Length == 0)
            return $"{name} — added:\n{Display(added)}";
        if (added.Length == 0)
            return $"{name} — removed:\n{Display(removed)}";
        return $"{name} — changed:\nBefore: {ChangeExcerpt(before, prefix, suffix)}\nAfter: {ChangeExcerpt(after, prefix, suffix)}";
    }

    private static string ChangeExcerpt(string text, int prefix, int suffix)
    {
        // Omit distant unchanged text, never the change itself. Include nearby context for partial-word edits.
        var start = Math.Max(0, prefix - 40);
        var end = text.Length - Math.Max(0, suffix - 40);
        if (InsideSurrogatePair(text, start))
            start--;
        if (InsideSurrogatePair(text, end))
            end++;
        return (start > 0 ? "…" : "") + Display(text[start..end]) + (end < text.Length ? "…" : "");
    }

    private static bool InsideSurrogatePair(string text, int index) =>
        index > 0 && index < text.Length && char.IsHighSurrogate(text[index - 1]) && char.IsLowSurrogate(text[index]);

    private static string Describe(LoreEntrySnapshot entry, bool compact = false) =>
        entry.Kind == "guild"
            ? $"{Display(entry.Name)}\n{Value(entry.Content, compact)}"
            : $"{Display(entry.Name)}\nPronouns: {Value(entry.Pronouns, compact)}\nNationality: {Value(entry.Nationality, compact)}\n"
                + $"Main character: {Value(entry.MainCharacter, compact)}\nAliases: {Value(string.Join(", ", entry.Aliases), compact)}\n"
                + $"Roles: {Value(string.Join(", ", entry.Roles), compact)}\nBiography: {Value(entry.Biography ?? "", compact)}";

    private static string Value(string text, bool compact)
    {
        if (!compact || text.Length <= 160)
            return Display(text);
        var length = char.IsHighSurrogate(text[159]) && char.IsLowSurrogate(text[160]) ? 159 : 160;
        return Display(text[..length]) + "…";
    }

    private static string Display(string text) => NotebookReview.DisplayText(text);
}
