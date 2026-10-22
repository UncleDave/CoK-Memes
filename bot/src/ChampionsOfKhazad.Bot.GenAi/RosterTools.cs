using System.ComponentModel;
using System.Text.Json;
using ChampionsOfKhazad.Bot.Roster;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class RosterTools(GuildRosterQueries roster, IMessageContext context, ILogger logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AIFunction[] Create() =>
        [
            AIFunctionFactory.Create(
                LeaderboardAsync,
                "get_guild_leaderboard",
                "Read the current guild's level leaderboard, or a profession leaderboard. Ten characters per page; shared ranks. Use this instead of lore for current levels/professions. Preserve mock status and observation age; data is not live. No Discord ownership, recipe knowledge, or achievement times can be inferred."
            ),
            AIFunctionFactory.Create(
                CharactersAsync,
                "find_guild_characters",
                "Find current guild characters by Firstname Lastname or a partial name. Forever has no realms. Return all relevant matches/pages rather than guessing which character is meant. Mock data must be labelled mock. Missing/failed data is not proof a character does not exist."
            ),
            AIFunctionFactory.Create(
                ProfessionMembersAsync,
                "find_guild_profession_members",
                "Find current guild characters with a profession, optionally at a minimum skill rank. Includes secondary professions when known. Follow pages and resolve ambiguous profession tiers. Unknown coverage is not proof nobody has a profession; ranks do not prove recipe knowledge."
            ),
        ];

    public Task<string> LeaderboardAsync(
        [Description("Optional profession name, skill-line ID, or ID:tier from an ambiguous result; omit for character level.")]
            string? profession = null,
        [Description("One-based page number. Each page has up to ten characters.")] int page = 1,
        CancellationToken cancellationToken = default
    ) => QueryAsync(page, profession, null, null, cancellationToken);

    public Task<string> CharactersAsync(
        [Description("Full or partial character name, up to 200 characters.")] string name,
        int page = 1,
        CancellationToken cancellationToken = default
    ) => string.IsNullOrWhiteSpace(name) ? Task.FromResult("Supply a character name.") : QueryAsync(page, null, name, null, cancellationToken);

    public Task<string> ProfessionMembersAsync(
        [Description("Profession name, skill-line ID, or ID:tier from an ambiguous result.")] string profession,
        [Description("Optional minimum skill rank, zero or greater.")] int? minimumSkill = null,
        int page = 1,
        CancellationToken cancellationToken = default
    ) =>
        string.IsNullOrWhiteSpace(profession)
            ? Task.FromResult("Supply a profession.")
            : QueryAsync(page, profession, null, minimumSkill, cancellationToken);

    private async Task<string> QueryAsync(int page, string? profession, string? name, int? minimumSkill, CancellationToken cancellationToken)
    {
        if (context.GuildId is not > 0 || context.ChannelId is not > 0)
            return "Roster lookups are only available in guild chat.";
        try
        {
            var result = await roster.QueryAsync(context.GuildId.Value, page, profession, name, minimumSkill, cancellationToken);
            return JsonSerializer.Serialize(
                new
                {
                    result.Status,
                    result.IsMock,
                    result.GuildName,
                    result.ObservedAtUtc,
                    result.CheckedAtUtc,
                    result.LastRefreshFailed,
                    result.IncompleteProfessionCoverage,
                    result.Page,
                    result.TotalPages,
                    result.TotalCount,
                    result.ProfessionChoices,
                    nextPage = result.Page < result.TotalPages ? (int?)(result.Page + 1) : null,
                    characters = result.Rows.Select(row => new
                    {
                        row.Rank,
                        row.Character.Id,
                        row.Character.FirstName,
                        row.Character.LastName,
                        row.Character.FullName,
                        row.Character.ClassName,
                        row.Character.Level,
                        row.Character.Professions,
                        row.Character.ProfessionRefreshFailed,
                        matchedProfession = row.Profession,
                    }),
                },
                JsonOptions
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Guild roster tool lookup failed");
            return "The roster lookup failed. Do not treat this as an empty guild or invent levels/professions. Ask the user to try again later.";
        }
    }
}
