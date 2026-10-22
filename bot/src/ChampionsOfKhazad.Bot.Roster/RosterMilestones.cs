using System.Security.Cryptography;
using System.Text;

namespace ChampionsOfKhazad.Bot.Roster;

public static class RosterMilestones
{
    public static (RosterProgress[] Progress, RosterNotice[] Notices) Compare(
        RosterState previous,
        RosterState current,
        GuildRosterOptions options,
        DateTime now,
        bool announcementsEnabled
    )
    {
        var progress = previous.Progress.ToDictionary(item => (item.CharacterKey, item.TrackKey));
        var existingMembers = previous.Characters.Select(member => member.Key).ToHashSet(StringComparer.Ordinal);
        var notices = new List<RosterNotice>();
        foreach (var member in current.Characters)
        {
            Check(
                "level",
                member.Level,
                null,
                Enumerable.Range(1, member.Level / 10).Select(value => value * 10).Concat(options.LevelCap is { } cap ? [cap] : []).Distinct()
            );
            foreach (var skill in member.Professions?.Skills ?? [])
                if (skill.Skill is { } rank)
                    Check(skill.TrackKey, rank, skill.Name, options.ProfessionMilestones);

            void Check(string track, int value, string? profession, IEnumerable<int> thresholds)
            {
                var key = (member.Key, track);
                var known = progress.GetValueOrDefault(key);
                var threshold = thresholds.Where(item => item > (known?.HighWater ?? value) && item <= value).DefaultIfEmpty().Max();
                if (announcementsEnabled && existingMembers.Contains(member.Key) && known is not null && threshold > 0)
                {
                    var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{current.Id}\n{member.Key}\n{track}\n{threshold}")));
                    notices.Add(
                        new RosterNotice
                        {
                            Id = id,
                            ScopeId = current.Id,
                            CharacterKey = member.Key,
                            CharacterName = member.FullName,
                            TrackKey = track,
                            ProfessionName = profession,
                            Threshold = threshold,
                            IsMock = current.IsMock,
                            CreatedAtUtc = now,
                        }
                    );
                }
                progress[key] = new RosterProgress(member.Key, track, Math.Max(value, known?.HighWater ?? value));
            }
        }
        return (progress.Values.ToArray(), notices.ToArray());
    }
}
