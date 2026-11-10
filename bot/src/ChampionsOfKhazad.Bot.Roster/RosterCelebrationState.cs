namespace ChampionsOfKhazad.Bot.Roster;

public record RosterCelebrationState
{
    public string[] Claimed { get; init; } = [];
}
