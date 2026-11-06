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
        if (page.ProfessionCoverage is { AffectedCharacters: > 0 } coverage)
        {
            var details = new List<string>();
            if (coverage.UnavailableCharacters > 0)
                details.Add($"{coverage.UnavailableCharacters} unavailable");
            if (coverage.PartialCharacters > 0)
                details.Add($"{coverage.PartialCharacters} partial");
            if (coverage.FailedRefreshCharacters > 0)
                details.Add($"{coverage.FailedRefreshCharacters} failed {(coverage.FailedRefreshCharacters == 1 ? "refresh" : "refreshes")}");
            if (coverage.UnknownRankCharacters > 0)
                details.Add($"{coverage.UnknownRankCharacters} unknown {(coverage.UnknownRankCharacters == 1 ? "rank" : "ranks")}");
            var scope = coverage.NameFiltered ? "Matching-character" : "Guild";
            lines.Add(
                $"{scope} profession coverage: {coverage.AffectedCharacters} of {coverage.TotalCharacters} {(coverage.TotalCharacters == 1 ? "character" : "characters")} affected ({string.Join(", ", details)})."
            );
        }
        var professionRows = page.Rows.Where(row => row.Profession is not null).ToArray();
        if (professionRows.Length > 0)
        {
            AddTimes(lines, "Profession checks (this page)", professionRows.Select(row => row.Character.ProfessionsCheckedAtUtc));
            AddTimes(lines, "Profession source timestamps (this page)", professionRows.Select(row => (DateTime?)row.Profession!.ObservedAtUtc));
            if (page.LastRefreshFailed)
                AddTimes(lines, "Roster check", [page.CheckedAtUtc]);
        }
        else
        {
            AddTimes(lines, "Roster check", [page.CheckedAtUtc]);
            if (page.Profession is null)
                AddTimes(lines, "Roster source timestamp", [page.ObservedAtUtc]);
        }
        if (page.LastRefreshFailed && page.ObservedAtUtc.HasValue)
            lines.Add("The latest refresh failed; showing the last collected data.");
        return Pack(lines).ToArray();
    }

    private static void AddTimes(List<string> lines, string label, IEnumerable<DateTime?> values)
    {
        var times = values.OfType<DateTime>().Order().ToArray();
        if (times.Length == 0)
            return;
        string Relative(DateTime timestamp) =>
            $"<t:{new DateTimeOffset(timestamp, TimeSpan.Zero).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}:R>";
        lines.Add(times[0] == times[^1] ? $"{label}: {Relative(times[0])}" : $"{label}: oldest {Relative(times[0])}; newest {Relative(times[^1])}");
    }

    public static IEnumerable<RosterNoticeBatch> NoticeBatches(IEnumerable<RosterNotice> notices)
    {
        var batch = new List<RosterNotice>();
        var lines = new List<string>();
        var length = 0;
        foreach (var notice in notices)
        {
            var line =
                (notice.IsMock ? "[Mock] " : "")
                + $"**{Safe(notice.CharacterName)}** reached "
                + (notice.ProfessionName is null ? $"level {notice.Threshold}!" : $"{notice.Threshold} {Safe(notice.ProfessionName)}!");
            if (length + line.Length + 1 > 1900 && batch.Count > 0)
            {
                yield return new(string.Join('\n', lines), batch.ToArray());
                batch.Clear();
                lines.Clear();
                length = 0;
            }
            batch.Add(notice);
            lines.Add(line);
            length += line.Length + 1;
        }
        if (batch.Count > 0)
            yield return new(string.Join('\n', lines), batch.ToArray());
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
