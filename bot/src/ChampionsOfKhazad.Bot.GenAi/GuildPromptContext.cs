namespace ChampionsOfKhazad.Bot.GenAi;

public static class GuildPromptContext
{
    public const string Identity = "'Champions of Khazad', a guild transitioning to World of Warcraft: Forever";

    public const string Activity = """
        The guild's Mists of Pandaria era has effectively ended. Do not assume ongoing MoP play, officer-organised raids/events,
        or regular in-game activity. The transition does not establish a Forever raid schedule or progression.
        Do not claim that nobody logs in, that every member has moved to Forever, or that the guild has disbanded.
        Guild update as of 8 October 2026: some members are already playing the Forever beta. Beta access requires paying
        roughly €60. The guild jokes that they paid far too much to 'daddy Blizzard'; the cost is real and approximate,
        while 'far too much' is the guild's comic judgment about that expense.
        Many members plan to play the release together at 00:00 CET on 5 November 2026, which is 23:00 GMT on
        4 November 2026 for members in the UK. This is planned group play, not a confirmed officer-organised raid
        or a guarantee that everyone will attend. Treat beta activity and release plans as dated context: do not keep
        describing the release as upcoming after that time, or claim that the planned gathering happened without evidence.
        Preserve past expansions and their anecdotes as guild history; do not relabel old events as Forever events.
        Use retrieved lore and current evidence for specific activity, schedules, and member details, distinguishing history
        from the present. Do not invent Forever mechanics, guild plans, or participation.
        """;

    public static string GetActivity(DateTimeOffset now) => $"Current UTC time: {now.UtcDateTime:yyyy-MM-dd HH:mm}.\n{Activity}";
}
