using ChampionsOfKhazad.Bot.GenAi;
using Discord;

namespace ChampionsOfKhazad.Bot;

internal static class NotebookReview
{
    public static Embed CreateEmbed(NotebookNote note)
    {
        var builder = new EmbedBuilder()
            .WithTitle($"Notebook candidate: {DisplayText(note.Subject, singleLine: true)}")
            .WithColor(Color.Gold)
            .WithDescription(DisplayText(note.Content))
            .AddField("Type", $"{note.Kind} — tentative, not canon", true)
            .AddField("Expires", $"<t:{new DateTimeOffset(note.ExpiresAtUtc, TimeSpan.Zero).ToUnixTimeSeconds()}:R>", true)
            .AddField("Why he chose to remember it", DisplayText(note.Reason))
            .AddField("Independent review", DisplayText(note.ReviewReason))
            .AddField("Requested during a response to", $"Discord user {note.RequestedBy}")
            .AddField("Review", $"Discard: `notebook discard {note.Id}`\nInspect: `notebook show {note.Id}`\nPause new notes: `notebook pause`")
            .WithFooter(
                $"Note {note.Id}. Delivery and activation are recorded separately; inspect its status with notebook show. Not an approval request."
            );
        foreach (var source in note.Sources)
            builder.AddField(
                "Source excerpt (quoted data)",
                $"{DisplayText(source.AuthorName, singleLine: true)} ({source.TimestampUtc:u})\n<{source.Url}>\n> {DisplayText(Excerpt(source.Content), singleLine: true)}"
            );
        var mentions = note.Sources.SelectMany(source => source.MentionedUsers).DistinctBy(user => user.Id).ToArray();
        if (mentions.Length > 0)
            builder.AddField(
                "Mentioned users (source labels)",
                string.Join(
                    '\n',
                    mentions.Take(4).Select(user => $"{DisplayText(user.Name ?? "name unavailable", singleLine: true)} — Discord user {user.Id}")
                ) + (mentions.Length > 4 ? "\nAdditional mentions: see source links." : string.Empty)
            );
        return builder.Build();
    }

    public static string Format(NotebookNote note, DateTime now) =>
        $"**{DisplayText(note.Subject, singleLine: true)}** ({note.Kind}; {Status(note, now)})\n{DisplayText(note.Content)}\n\n"
        + $"Why he chose it: {DisplayText(note.Reason)}\nIndependent review: {DisplayText(note.ReviewReason)}\nRequested by: {note.RequestedBy}\nCreated: {note.CreatedAtUtc:u}\nExpires: {note.ExpiresAtUtc:u}\n"
        + $"Sources:\n{string.Join('\n', note.Sources.Select(source => $"- {DisplayText(source.AuthorName, singleLine: true)}: <{source.Url}>"))}\n"
        + $"Discard: `notebook discard {note.Id}`";

    public static string Status(NotebookNote note, DateTime now) =>
        note.DiscardedAtUtc is not null ? "discarded"
        : note.ExpiresAtUtc <= now ? "expired"
        : note.ReviewDelivered ? "active"
        : "audit-only; review/activation not confirmed";

    internal static string DisplayText(string text, bool singleLine = false) =>
        Discord
            .Format.Sanitize(singleLine ? text.Replace('\r', ' ').Replace('\n', ' ') : text)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal)
            .Replace("@", "@\u200b", StringComparison.Ordinal);

    internal static string Excerpt(string text)
    {
        var length = Math.Min(text.Length, 160);
        if (length < text.Length && length > 0 && char.IsHighSurrogate(text[length - 1]) && char.IsLowSurrogate(text[length]))
            length--;
        return text[..length] + (length < text.Length ? "…" : string.Empty);
    }
}
