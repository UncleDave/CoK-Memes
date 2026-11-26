namespace ChampionsOfKhazad.Bot.Roster;

public class GuildRosterQueries(GuildRosterOptions options, GuildRosterService service, IRosterStore store)
{
    public const int PageSize = 10;
    public bool Enabled => options.Enabled;

    public bool IsAvailableFor(ulong guildId) => options.Enabled && guildId == options.GuildId;

    public async Task<RecipePage> FindRecipeCraftersAsync(ulong guildId, string recipe, int page = 1, CancellationToken cancellationToken = default)
    {
        if (!IsAvailableFor(guildId))
            return new() { Status = "unavailable" };
        recipe = recipe?.Trim() ?? "";
        if (page < 1 || recipe.Length is 0 or > 200 || recipe.Any(char.IsControl))
            return new() { Status = "invalid_query" };
        var state = await store.GetAsync(service.ScopeId, cancellationToken);
        if (state?.ObservedAtUtc is null)
            return new()
            {
                Status = state?.LastRefreshFailed == true ? "refresh_failed" : "no_data",
                IsMock = service.IsMock,
                LastRefreshFailed = state?.LastRefreshFailed ?? false,
            };

        var unavailable = state.Characters.Where(member => member.Professions is null).ToArray();
        var partial = state
            .Characters.Where(member =>
                member.Professions is { } professions && (!professions.Complete || professions.Skills.Any(skill => skill.KnownRecipes is null))
            )
            .ToArray();
        var failed = state.Characters.Where(member => member.ProfessionRefreshFailed).ToArray();
        var result = new RecipePage
        {
            IsMock = state.IsMock,
            GuildName = state.GuildName,
            LastRefreshFailed = state.LastRefreshFailed,
            Page = page,
            Coverage = new()
            {
                TotalCharacters = state.Characters.Length,
                AffectedCharacters = unavailable.Concat(partial).Concat(failed).DistinctBy(member => member.Key).Count(),
                UnavailableCharacters = unavailable.Length,
                PartialCharacters = partial.Length,
                FailedRefreshCharacters = failed.Length,
            },
        };
        var known = state
            .Characters.SelectMany(member =>
                (member.Professions?.Skills ?? []).SelectMany(skill =>
                    (skill.KnownRecipes ?? []).Select(knownRecipe => (Member: member, Skill: skill, Recipe: knownRecipe))
                )
            )
            .ToArray();
        var matches = known.Where(item => string.Equals(item.Recipe.Id, recipe, StringComparison.Ordinal)).ToArray();
        if (matches.Length == 0)
            matches = known.Where(item => string.Equals(item.Recipe.Name, recipe, StringComparison.OrdinalIgnoreCase)).ToArray();
        // A supplied ID or full recipe name wins over a broader substring match.
        if (matches.Length == 0)
            matches = known.Where(item => item.Recipe.Name.Contains(recipe, StringComparison.OrdinalIgnoreCase)).ToArray();
        var choices = matches
            .Select(item => item.Recipe)
            .DistinctBy(item => item.Id)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        if (choices.Length == 0)
            return result with { Status = state.Characters.Length == 0 ? "empty" : "no_match" };
        if (choices.Length > 1)
        {
            var choicePages = (choices.Length + PageSize - 1) / PageSize;
            return result with
            {
                Status = page > choicePages ? "page_out_of_range" : "ambiguous_recipe",
                TotalCount = choices.Length,
                TotalPages = choicePages,
                RecipeChoices = page > choicePages ? [] : choices.Skip((page - 1) * PageSize).Take(PageSize).ToArray(),
            };
        }
        var crafters = known
            .Where(item => item.Recipe.Id == choices[0].Id)
            .OrderBy(item => item.Member.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Member.Key, StringComparer.Ordinal)
            .ThenByDescending(item => item.Skill.ObservedAtUtc)
            .ThenBy(item => item.Skill.TrackKey, StringComparer.Ordinal)
            .DistinctBy(item => item.Member.Key)
            .ToArray();
        var totalPages = (crafters.Length + PageSize - 1) / PageSize;
        return result with
        {
            Status = page > totalPages ? "page_out_of_range" : "ok",
            Recipe = choices[0],
            TotalCount = crafters.Length,
            TotalPages = totalPages,
            Crafters =
                page > totalPages
                    ? []
                    : crafters
                        .Skip((page - 1) * PageSize)
                        .Take(PageSize)
                        .Select(item => new RecipeCrafter
                        {
                            Id = item.Member.Id,
                            FullName = item.Member.FullName,
                            ClassName = item.Member.ClassName,
                            Level = item.Member.Level,
                            Profession = item.Skill.Name,
                            ProfessionId = item.Skill.Id,
                            Tier = item.Skill.Tier,
                            Skill = item.Skill.Skill,
                            ObservedAtUtc = item.Skill.ObservedAtUtc,
                            CheckedAtUtc = item.Member.ProfessionsCheckedAtUtc,
                            RefreshFailed = item.Member.ProfessionRefreshFailed,
                        })
                        .ToArray(),
        };
    }

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
            MilestoneValue = profession is null ? options.LevelCap : RosterMilestones.ProfessionCelebrationRank,
            Profession = profession,
        };
        var members = state.Characters.Where(member => name is null || member.FullName.Contains(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        var entries = new List<(RosterCharacter Member, ProfessionSkill? Profession, int Value)>();
        var trackKey = RosterProgress.LevelTrackKey;
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
            var unavailable = members.Where(member => member.Professions is null).ToArray();
            var partial = members.Where(member => member.Professions is { Complete: false }).ToArray();
            var failed = members.Where(member => member.ProfessionRefreshFailed).ToArray();
            var unknown = matches.Where(item => item.Skill.Skill is null).Select(item => item.Member).DistinctBy(member => member.Key).ToArray();
            result = result with
            {
                ProfessionCoverage = new()
                {
                    NameFiltered = name is not null,
                    TotalCharacters = members.Length,
                    AffectedCharacters = unavailable.Concat(partial).Concat(failed).Concat(unknown).DistinctBy(member => member.Key).Count(),
                    UnavailableCharacters = unavailable.Length,
                    PartialCharacters = partial.Length,
                    FailedRefreshCharacters = failed.Length,
                    UnknownRankCharacters = unknown.Length,
                },
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
            if (tracks.Length == 1)
                trackKey = tracks[0];
            entries.AddRange(
                matches
                    .Where(item => item.Skill.Skill is { } rank && rank >= (minimumSkill ?? 0))
                    .Select(item => (item.Member, (ProfessionSkill?)item.Skill, item.Skill.Skill!.Value))
            );
        }
        if (entries.Count == 0)
            return result with { Status = state.Characters.Length == 0 ? "empty" : "no_match" };

        var recordedProgress = state
            .Progress.Where(item => item.TrackKey == trackKey && item.RecordedAtUtc.HasValue)
            .ToDictionary(item => item.CharacterKey, StringComparer.Ordinal);
        DateTime? RecordedAt(RosterCharacter member, int value) =>
            recordedProgress.GetValueOrDefault(member.Key) is { } progress && progress.HighWater == value ? progress.RecordedAtUtc : null;

        var ordered = entries
            .Select(item => (item.Member, item.Profession, item.Value, RecordedAtUtc: RecordedAt(item.Member, item.Value)))
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.RecordedAtUtc is null)
            .ThenBy(item => item.RecordedAtUtc)
            .ThenBy(item => item.Member.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Member.Key, StringComparer.Ordinal)
            .ToArray();
        var totalPages = (ordered.Length + PageSize - 1) / PageSize;
        result = result with { TotalPages = totalPages, TotalCount = ordered.Length };
        if (page > totalPages)
            return result with { Status = "page_out_of_range" };
        var rows = new List<RosterEntry>();
        var rank = 0;
        for (var index = 0; index < ordered.Length; index++)
        {
            var item = ordered[index];
            if (index == 0 || item.Value != ordered[index - 1].Value || item.RecordedAtUtc != ordered[index - 1].RecordedAtUtc)
                rank = index + 1;
            if (index / PageSize + 1 == page)
                rows.Add(new(rank, item.Member, item.Profession) { RecordedAtUtc = item.RecordedAtUtc });
        }
        return result with
        {
            Rows =
                profession is null && result.MilestoneValue is { } cap && rows.Any(row => row.Character.Level == cap && row.RecordedAtUtc.HasValue)
                    ? WithClassFirsts(rows, state.Characters, cap, member => RecordedAt(member, cap))
                    : rows.ToArray(),
        };
    }

    private static RosterEntry[] WithClassFirsts(
        List<RosterEntry> rows,
        RosterCharacter[] members,
        int cap,
        Func<RosterCharacter, DateTime?> recordedAtCap
    )
    {
        var firstByClass = members
            .Where(member => member.Level == cap)
            .Select(member => (member.ClassName, RecordedAtUtc: recordedAtCap(member)))
            .Where(item => item.RecordedAtUtc.HasValue)
            .GroupBy(item => item.ClassName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Min(item => item.RecordedAtUtc), StringComparer.OrdinalIgnoreCase);
        return rows.Select(row =>
                row.Character.Level == cap
                && row.RecordedAtUtc is { } recordedAt
                && recordedAt == firstByClass.GetValueOrDefault(row.Character.ClassName)
                    ? row with
                    {
                        FirstClassAtCap = true,
                    }
                    : row
            )
            .ToArray();
    }
}
