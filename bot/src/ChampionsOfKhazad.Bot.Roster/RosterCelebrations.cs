using System.Security.Cryptography;
using System.Text;

namespace ChampionsOfKhazad.Bot.Roster;

internal static class RosterCelebrations
{
    private static readonly int[] LevelCounts = [5, 10, 20];
    private static readonly string[] Classes = ["Druid", "Hunter", "Mage", "Paladin", "Priest", "Rogue", "Shaman", "Warlock", "Warrior"];
    private static readonly string[] ProfessionIds = ["171", "164", "333", "202", "182", "165", "186", "393", "197", "185", "356", "129"];

    public static (RosterCelebrationState State, RosterNotice[] Notices) Compare(
        RosterState previous,
        RosterState current,
        GuildRosterOptions options,
        DateTime now,
        bool announcementsEnabled,
        RosterNotice[] notices
    )
    {
        var claimed = previous.Celebrations?.Claimed.ToHashSet(StringComparer.Ordinal) ?? Seed(previous, options);
        var result = notices.ToDictionary(notice => notice.Id, StringComparer.Ordinal);
        var guildHonours = new List<string>();
        var guildKeys = new List<string>();
        var canAnnounce = announcementsEnabled && previous.ObservedAtUtc.HasValue;

        if (options.LevelCap is { } cap)
        {
            var capped = current.Characters.Where(member => member.Level >= cap).ToArray();
            First($"first:level:{cap}", $"level {cap} in the guild", capped, "level", cap);
            foreach (var group in capped.GroupBy(member => member.ClassName, StringComparer.OrdinalIgnoreCase))
                First(ClassKey(group.Key, cap), $"{group.Key} at level {cap}", group.ToArray(), "level", cap);

            var levelCrossed = notices.Any(notice => notice.TrackKey == "level" && notice.Threshold == cap);
            var crossedCounts = LevelCounts.Where(count => capped.Length >= count && claimed.Add(CountKey(count, cap))).ToArray();
            if (crossedCounts.Length > 0 && levelCrossed)
                Guild(CountKey(crossedCounts[^1], cap), $"{crossedCounts[^1]} guild characters at level {cap}");
            if (AllClasses(current, options) && claimed.Add($"all-classes:{cap}") && levelCrossed)
                Guild($"all-classes:{cap}", $"Every class at level {cap}");
        }

        foreach (var group in Skills(current).Where(item => item.Skill.Skill >= 300).GroupBy(item => item.Skill.TrackKey, StringComparer.Ordinal))
            First($"first:{group.Key}:300", $"300 {group.First().Skill.Name}", group.Select(item => item.Member).ToArray(), group.Key, 300);

        if (
            AllProfessions(current)
            && claimed.Add("all-professions:300")
            && notices.Any(notice => notice.ProfessionName is not null && notice.Threshold == 300)
        )
            Guild("all-professions:300", "Every profession at 300");

        if (canAnnounce && guildHonours.Count > 0)
            result.Add(
                "guild",
                new RosterNotice
                {
                    Id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{current.Id}\nguild\n{string.Join('\n', guildKeys.Order())}"))),
                    ScopeId = current.Id,
                    CharacterKey = "",
                    CharacterName = "",
                    TrackKey = "guild",
                    Celebration = RosterCelebrationKind.Guild,
                    Honours = guildHonours.ToArray(),
                    IsMock = current.IsMock,
                    CreatedAtUtc = now,
                }
            );
        return (new() { Claimed = claimed.Order(StringComparer.Ordinal).ToArray() }, result.Values.ToArray());

        void First(string key, string description, RosterCharacter[] qualified, string track, int threshold)
        {
            if (qualified.Length == 0 || !claimed.Add(key) || !canAnnounce)
                return;
            var candidates = result.Values.Where(notice => notice.TrackKey == track && notice.Threshold == threshold).ToArray();
            var keys = qualified.Select(member => member.Key).ToHashSet(StringComparer.Ordinal);
            var winners = candidates.Where(notice => keys.Contains(notice.CharacterKey)).ToArray();
            // A silently baselined arrival/previously unknown skill cannot establish an ordered first.
            if (winners.Length != keys.Count)
                return;
            var heading = $"{(winners.Length > 1 ? "Joint first recorded" : "First recorded")} {description}";
            foreach (var winner in winners)
            {
                var latest = result[winner.Id];
                result[winner.Id] = latest with { Celebration = RosterCelebrationKind.First, Honours = [.. latest.Honours, heading] };
            }
        }

        void Guild(string key, string heading)
        {
            guildKeys.Add(key);
            guildHonours.Add(heading);
        }
    }

    private static HashSet<string> Seed(RosterState previous, GuildRosterOptions options)
    {
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        if (options.LevelCap is { } cap)
        {
            var capped = previous.Characters.Where(member => member.Level >= cap).ToArray();
            var historical = previous.Progress.Where(progress => progress.TrackKey == "level" && progress.HighWater >= cap).ToArray();
            if (capped.Length > 0 || historical.Length > 0)
                claimed.Add($"first:level:{cap}");
            var members = previous.Characters.ToDictionary(member => member.Key, StringComparer.Ordinal);
            foreach (var member in capped)
                claimed.Add(ClassKey(member.ClassName, cap));
            foreach (var progress in historical)
                if (members.TryGetValue(progress.CharacterKey, out var member))
                    claimed.Add(ClassKey(member.ClassName, cap));
                else
                    // Legacy high-water marks do not contain a departed character's class.
                    foreach (var className in Classes)
                        claimed.Add(ClassKey(className, cap));
            foreach (var count in LevelCounts.Where(count => capped.Length >= count))
                claimed.Add(CountKey(count, cap));
            if (AllClasses(previous, options))
                claimed.Add($"all-classes:{cap}");
        }
        foreach (
            var progress in previous.Progress.Where(progress =>
                progress.TrackKey.StartsWith("profession:", StringComparison.Ordinal) && progress.HighWater >= 300
            )
        )
            claimed.Add($"first:{progress.TrackKey}:300");
        foreach (var item in Skills(previous).Where(item => item.Skill.Skill >= 300))
            claimed.Add($"first:{item.Skill.TrackKey}:300");
        if (AllProfessions(previous))
            claimed.Add("all-professions:300");
        return claimed;
    }

    private static bool AllClasses(RosterState state, GuildRosterOptions options) =>
        options.LevelCap is { } cap
        && Classes.All(className =>
            state.Characters.Any(member => member.Level >= cap && string.Equals(member.ClassName, className, StringComparison.OrdinalIgnoreCase))
        );

    private static bool AllProfessions(RosterState state)
    {
        var capped = Skills(state).Where(item => item.Skill.Skill >= 300).Select(item => item.Skill.Id).ToHashSet(StringComparer.Ordinal);
        return ProfessionIds.All(capped.Contains);
    }

    private static IEnumerable<(RosterCharacter Member, ProfessionSkill Skill)> Skills(RosterState state) =>
        state.Characters.SelectMany(member => (member.Professions?.Skills ?? []).Select(skill => (member, skill)));

    private static string ClassKey(string name, int cap) => $"first:class:{name.ToUpperInvariant()}:{cap}";

    private static string CountKey(int count, int cap) => $"count:level:{cap}:{count}";
}
