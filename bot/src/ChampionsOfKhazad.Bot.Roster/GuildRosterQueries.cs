namespace ChampionsOfKhazad.Bot.Roster;

public class GuildRosterQueries(GuildRosterOptions options, GuildRosterService service, IRosterStore store)
{
    public const int PageSize = 10;
    public bool Enabled => options.Enabled;

    public bool IsAvailableFor(ulong guildId) => options.Enabled && guildId == options.GuildId;

    public async Task<RosterPage> QueryAsync(
        ulong guildId,
        int page = 1,
        string? profession = null,
        string? name = null,
        int? minimumSkill = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsAvailableFor(guildId))
            return new() { Status = "unavailable" };
        profession = string.IsNullOrWhiteSpace(profession) ? null : profession.Trim();
        name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (page < 1 || profession?.Length > 100 || name?.Length > 200 || minimumSkill is < 0 || (minimumSkill.HasValue && profession is null))
            return new() { Status = "invalid_query" };
        var state = await store.GetAsync(service.ScopeId, cancellationToken);
        if (state?.ObservedAtUtc is null)
            return new()
            {
                Status = state?.LastRefreshFailed == true ? "refresh_failed" : "no_data",
                IsMock = service.IsMock,
                CheckedAtUtc = state?.CheckedAtUtc,
                LastRefreshFailed = state?.LastRefreshFailed ?? false,
            };
        var result = new RosterPage
        {
            IsMock = state.IsMock,
            GuildName = state.GuildName,
            ObservedAtUtc = state.ObservedAtUtc,
            CheckedAtUtc = state.CheckedAtUtc,
            LastRefreshFailed = state.LastRefreshFailed,
            Page = page,
            Profession = profession,
        };
        var members = state.Characters.Where(member => name is null || member.FullName.Contains(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        var entries = new List<(RosterCharacter Member, ProfessionSkill? Profession, int Value)>();
        if (profession is null)
            entries.AddRange(members.Select(member => (member, (ProfessionSkill?)null, member.Level)));
        else
        {
            bool Matches(ProfessionSkill skill) =>
                string.Equals(skill.Id, profession, StringComparison.OrdinalIgnoreCase)
                || string.Equals(skill.Name, profession, StringComparison.OrdinalIgnoreCase)
                || string.Equals($"{skill.Id}:{skill.Tier}", profession, StringComparison.OrdinalIgnoreCase);
            var matches = members
                .SelectMany(member => (member.Professions?.Skills ?? []).Where(Matches).Select(skill => (Member: member, Skill: skill)))
                .ToArray();
            var tracks = matches.Select(item => item.Skill.TrackKey).Distinct(StringComparer.Ordinal).ToArray();
            result = result with
            {
                IncompleteProfessionCoverage = members.Any(member =>
                    member.Professions is null
                    || !member.Professions.Complete
                    || member.ProfessionRefreshFailed
                    || member.Professions.Skills.Any(skill => Matches(skill) && skill.Skill is null)
                ),
            };
            if (tracks.Length > 1)
            {
                var choices = matches.Select(item => $"{item.Skill.Id}:{item.Skill.Tier}").Distinct().Order(StringComparer.Ordinal).ToArray();
                var pages = (choices.Length + PageSize - 1) / PageSize;
                return result with
                {
                    Status = page > pages ? "page_out_of_range" : "ambiguous_profession",
                    TotalCount = choices.Length,
                    TotalPages = pages,
                    ProfessionChoices = page > pages ? [] : choices.Skip((page - 1) * PageSize).Take(PageSize).ToArray(),
                };
            }
            entries.AddRange(
                matches
                    .Where(item => item.Skill.Skill is { } rank && rank >= (minimumSkill ?? 0))
                    .Select(item => (item.Member, (ProfessionSkill?)item.Skill, item.Skill.Skill!.Value))
            );
        }
        var ordered = entries
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Member.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Member.Key, StringComparer.Ordinal)
            .ToArray();
        var totalPages = (ordered.Length + PageSize - 1) / PageSize;
        result = result with { TotalPages = totalPages, TotalCount = ordered.Length };
        if (ordered.Length == 0)
            return result with { Status = state.Characters.Length == 0 ? "empty" : "no_match" };
        if (page > totalPages)
            return result with { Status = "page_out_of_range" };
        var rows = new List<RosterEntry>();
        var rank = 0;
        for (var index = 0; index < ordered.Length; index++)
        {
            if (index == 0 || ordered[index].Value != ordered[index - 1].Value)
                rank = index + 1;
            if (index / PageSize + 1 == page)
                rows.Add(new(rank, ordered[index].Member, ordered[index].Profession));
        }
        return result with { Rows = rows.ToArray() };
    }
}
