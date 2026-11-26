using System.Globalization;
using System.Text.RegularExpressions;

namespace ChampionsOfKhazad.Bot.Roster;

public static partial class RosterText
{
    private static readonly string[] LevelTaglines =
    [
        "Good. Now you can die in more expensive gear.",
        "The boars of the Barrens are calling it a public holiday.",
        "You reached the end of the grind. That was the joke.",
        "You may now spend forty minutes outside a dungeon with the best of them.",
        "The fire doesn't check your level before you stand in it. I asked.",
        "The flight master heard the news. As a mark of respect, the fare is exactly the same.",
        "You earned a round of applause. Don't try to redeem it for chips.",
        "Somewhere a quest giver just remembered six more errands.",
    ];

    private static readonly string[] ProfessionTaglines =
    [
        "Three hundred. You may now call your mistakes technique.",
        "Your prize is twelve whispers that start with 'quick question'.",
        "That's not a bank alt anymore. That's a warehouse with a name.",
        "Good. Now you can charge guild prices: same as normal, but with an argument.",
        "Your tools asked for the day off. I told them to ask you.",
        "Don't brag to the trainer. They'll find a way to charge for listening.",
        "At last, an expert. Can you get the burnt bits off this pan?",
        "The first 299 points took patience. The last one was spite.",
    ];

    private static readonly string[] MixedTaglines =
    [
        "Levels and trades. You're all becoming dangerously employable.",
        "One hand on the weapon, one on the toolbox. Who's holding the chips?",
        "The guild can fight and craft. Watch us still need a mage for water.",
        "Two kinds of progress in one ledger entry. I'm charging the ink to the guild bank.",
        "All that levelling and trade work, and someone's still going to ask for spare linen.",
        "Enough muscle and know-how to build a raid boss. Please don't.",
    ];

    private static readonly string[] GuildTaglines =
    [
        "I'd buy you all a round, but that's how guilds go bankrupt.",
        "Good work. Nobody tell the Alliance we can count.",
        "That's teamwork. I was beginning to think it was a rumour started by the raid leader.",
        "Group photo. Anyone still on a flight path can be a smudge on the horizon.",
        "Look at that. You lot can coordinate when there's no ready check to ignore.",
        "For once, the ledger has something in it besides names and amounts owed.",
    ];

    public static string Safe(string value) => Markdown().Replace(value, @"\$1").Replace("@", "@\u200b", StringComparison.Ordinal);

    public static string[] Leaderboard(RosterPage page)
    {
        var lines = new List<string>();
        if (page.IsMock)
            lines.Add("**Mock roster**");
        if (page.Status != "ok")
        {
            lines.Add(
                page.Status switch
                {
                    "no_data" => "No roster has been collected yet.",
                    "refresh_failed" => "The roster refresh failed; no collected roster is available yet.",
                    "empty" => "The collected guild roster is empty.",
                    "no_match" => "No matching characters with known ranks were found.",
                    "page_out_of_range" => $"Choose a page from 1 to {page.TotalPages}.",
                    "invalid_query" => "Use a positive page number and a valid profession.",
                    "ambiguous_profession" => "Choose a profession tier:",
                    _ => "The guild roster is not available here.",
                }
            );
            if (page.Status == "ambiguous_profession")
            {
                lines.AddRange(page.ProfessionChoices.Select(Safe));
                lines.Add($"Page {page.Page}/{page.TotalPages}");
            }
        }
        else
        {
            lines.Add($"**{Safe(page.GuildName)} — {(page.Profession is null ? "Level leaderboard" : Safe(page.Profession) + " leaderboard")}**");
            lines.AddRange(page.Rows.Select(row => LeaderboardLine(row, page.MilestoneValue)));
            if (page.MilestoneValue is { } milestone && page.Rows.Any(row => row.FirstAtMilestone || row.FirstClassAtCap))
                lines.Add(page.Profession is null ? $"First recorded at {milestone}: 🏆 guild · 🥇 class" : $"🏆 First recorded at {milestone}");
            lines.Add($"Page {page.Page}/{page.TotalPages}");
        }
        var checkedAt = page.Profession is null
            ? page.CheckedAtUtc
            : page.Rows.Where(row => row.Profession is not null).Min(row => row.Character.ProfessionsCheckedAtUtc);
        if (checkedAt is { } timestamp)
            lines.Add($"Last checked {DiscordDate(timestamp)}");
        var failedProfessionChecks = page.Rows.Count(row => row.Profession is not null && row.Character.ProfessionRefreshFailed);
        if (failedProfessionChecks > 0)
            lines.Add(
                $"The latest profession check failed for {failedProfessionChecks} displayed {(failedProfessionChecks == 1 ? "character" : "characters")}; showing retained data."
            );
        if (page.LastRefreshFailed && page.ObservedAtUtc.HasValue)
            lines.Add("The latest refresh failed; showing the last collected data.");
        return Pack(lines).ToArray();
    }

    private static string LeaderboardLine(RosterEntry row, int? milestone)
    {
        var trophy = row.FirstAtMilestone ? " 🏆" : "";
        var className = Safe(row.Character.ClassName) + (row.FirstClassAtCap ? " 🥇" : "");
        var value = row.Profession is { } skill
            ? $"{Safe(skill.Name)} {skill.Skill}" + (skill.MaxSkill is { } max ? $"/{max}" : "")
            : $"level {row.Character.Level}";
        var line = $"{row.Rank}. **{Safe(row.Character.FullName)}**{trophy} — {className}, {value}";
        if (milestone.HasValue && row.Value == milestone)
            line += row.RecordedAtUtc is { } recordedAt ? $" · recorded {DiscordDate(recordedAt)}" : " · time unknown";
        return line;
    }

    private static string DiscordDate(DateTime timestamp) =>
        $"<t:{new DateTimeOffset(timestamp, TimeSpan.Zero).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}:f>";

    public static IEnumerable<RosterNoticeBatch> NoticeBatches(IEnumerable<RosterNotice> notices, Random? random = null)
    {
        random ??= Random.Shared;
        var items = notices.ToArray();
        foreach (
            var observation in items
                .Where(notice => notice.Celebration != RosterCelebrationKind.None)
                .GroupBy(notice => (notice.CreatedAtUtc, notice.IsMock))
        )
        foreach (
            var batch in PackNotices(
                observation.OrderBy(notice => notice.CharacterName, StringComparer.Ordinal).ThenBy(notice => notice.TrackKey, StringComparer.Ordinal),
                random,
                celebration: true
            )
        )
            yield return batch;
        foreach (var batch in PackNotices(items.Where(notice => notice.Celebration == RosterCelebrationKind.None), random, celebration: false))
            yield return batch;
    }

    private static IEnumerable<RosterNoticeBatch> PackNotices(IEnumerable<RosterNotice> notices, Random random, bool celebration)
    {
        var batch = new List<RosterNotice>();
        var lines = new List<string>();
        var length = 0;
        var limit = celebration ? 1700 : 1900;
        foreach (var notice in notices)
        {
            var line = celebration
                ? CelebrationLine(notice)
                : (notice.IsMock ? "[Mock] " : "")
                    + $"**{Safe(notice.CharacterName)}** reached "
                    + (notice.ProfessionName is null ? $"level {notice.Threshold}!" : $"{notice.Threshold} {Safe(notice.ProfessionName)}!");
            if (line.Length > limit)
                throw new InvalidDataException("Roster notice exceeds Discord's message limit.");
            if (length + line.Length + 1 > limit && batch.Count > 0)
            {
                yield return NoticeBatch(lines, batch, random, celebration);
                batch.Clear();
                lines.Clear();
                length = 0;
            }
            batch.Add(notice);
            lines.Add(line);
            length += line.Length + 1;
        }
        if (batch.Count > 0)
            yield return NoticeBatch(lines, batch, random, celebration);
    }

    private static string CelebrationLine(RosterNotice notice)
    {
        var honours = notice.Honours.Select(heading => $"🏆 **{Safe(heading)}**");
        if (notice.Celebration == RosterCelebrationKind.Guild)
            return string.Join('\n', honours);
        var achievement = notice.ProfessionName is null ? $"level {notice.Threshold}" : $"{notice.Threshold} {Safe(notice.ProfessionName)}";
        var characterClass = notice.ProfessionName is null && notice.ClassName is { } name ? $" · {Safe(name)}" : "";
        return $"**{Safe(notice.CharacterName)}** — **{achievement}**{characterClass}"
            + (notice.Honours.Length > 0 ? "\n" + string.Join('\n', honours) : "");
    }

    private static RosterNoticeBatch NoticeBatch(List<string> lines, List<RosterNotice> notices, Random random, bool celebration)
    {
        var text = string.Join('\n', lines);
        if (!celebration)
            return new(text, notices.ToArray());
        var first = notices.Any(notice => notice.Celebration == RosterCelebrationKind.First);
        var guild = notices.Any(notice => notice.Celebration == RosterCelebrationKind.Guild);
        var levels = notices.Where(notice => notice.TrackKey == "level").ToArray();
        var professions = notices.Any(notice => notice.ProfessionName is not null);
        var title =
            first ? "Guild firsts — Grub's honours ledger"
            : guild ? "Guild milestone — Grub's honours ledger"
            : levels.Length > 0 && professions ? "Strength and mastery earned"
            : levels.Length > 0 ? $"Level {levels[0].Threshold} — strength earned"
            : "Skill 300 — mastery earned";
        var taglines =
            guild ? GuildTaglines
            : levels.Length == 0 ? ProfessionTaglines
            : professions ? MixedTaglines
            : LevelTaglines;
        var tagline = taglines[random.Next(taglines.Length)];
        return new(
            text + $"\n\n*“{tagline}” — Grub*",
            notices.ToArray(),
            (notices[0].IsMock ? "[Mock] " : "") + title,
            first ? 0xF1C40Fu
                : guild ? 0x9B59B6u
                : 0x2ECC71u
        );
    }

    private static IEnumerable<string> Pack(IEnumerable<string> lines)
    {
        var text = "";
        foreach (var line in lines)
        {
            if (line.Length > 1900)
                throw new InvalidDataException("Roster line exceeds Discord's message limit.");
            if (text.Length + line.Length + 1 > 1900)
            {
                yield return text;
                text = "";
            }
            text += (text.Length == 0 ? "" : "\n") + line;
        }
        if (text.Length > 0)
            yield return text;
    }

    [GeneratedRegex(@"([\\`*_{}\[\]()<>|~])")]
    private static partial Regex Markdown();
}
