using System.Globalization;
using System.Text.RegularExpressions;

namespace ChampionsOfKhazad.Bot.Roster;

public static partial class RosterText
{
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
            lines.AddRange(
                page.Rows.Select(row =>
                    $"{row.Rank}. **{Safe(row.Character.FullName)}** — {Safe(row.Character.ClassName)}, "
                    + (
                        row.Profession is null
                            ? $"level {row.Character.Level}"
                            : $"{Safe(row.Profession.Name)} {row.Profession.Skill}" + (row.Profession.MaxSkill is { } max ? $"/{max}" : "")
                    )
                )
            );
            lines.Add($"Page {page.Page}/{page.TotalPages}");
        }
        var professionRows = page.Rows.Where(row => row.Profession is not null).ToArray();
        var checkedAt = page.Profession is null ? page.CheckedAtUtc : professionRows.Select(row => row.Character.ProfessionsCheckedAtUtc).Min();
        if (checkedAt is { } timestamp)
            lines.Add(
                $"Last checked <t:{new DateTimeOffset(timestamp, TimeSpan.Zero).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}:f>"
            );
        var failedProfessionChecks = professionRows.Count(row => row.Character.ProfessionRefreshFailed);
        if (failedProfessionChecks > 0)
            lines.Add(
                $"The latest profession check failed for {failedProfessionChecks} displayed {(failedProfessionChecks == 1 ? "character" : "characters")}; showing retained data."
            );
        if (page.LastRefreshFailed && page.ObservedAtUtc.HasValue)
            lines.Add("The latest refresh failed; showing the last collected data.");
        return Pack(lines).ToArray();
    }

    public static IEnumerable<RosterNoticeBatch> NoticeBatches(IEnumerable<RosterNotice> notices)
    {
        var items = notices.ToArray();
        foreach (
            var observation in items
                .Where(notice => notice.Celebration != RosterCelebrationKind.None)
                .GroupBy(notice => (notice.CreatedAtUtc, notice.IsMock))
        )
        foreach (
            var batch in PackNotices(
                observation.OrderBy(notice => notice.CharacterName, StringComparer.Ordinal).ThenBy(notice => notice.TrackKey, StringComparer.Ordinal),
                celebration: true
            )
        )
            yield return batch;
        foreach (var batch in PackNotices(items.Where(notice => notice.Celebration == RosterCelebrationKind.None), celebration: false))
            yield return batch;
    }

    private static IEnumerable<RosterNoticeBatch> PackNotices(IEnumerable<RosterNotice> notices, bool celebration)
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
                yield return NoticeBatch(lines, batch, celebration);
                batch.Clear();
                lines.Clear();
                length = 0;
            }
            batch.Add(notice);
            lines.Add(line);
            length += line.Length + 1;
        }
        if (batch.Count > 0)
            yield return NoticeBatch(lines, batch, celebration);
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

    private static RosterNoticeBatch NoticeBatch(List<string> lines, List<RosterNotice> notices, bool celebration)
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
        var commendation =
            guild ? "Strong hands. Strong guild. Stand together."
            : professions && levels.Length == 0 ? "Skill earned through work. Put it to use."
            : "Strength earned. Stand proud.";
        return new(
            text + $"\n\n*“{commendation}” — Grub*",
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
