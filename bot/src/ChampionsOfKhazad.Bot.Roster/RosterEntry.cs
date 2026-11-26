namespace ChampionsOfKhazad.Bot.Roster;

public record RosterEntry(int Rank, RosterCharacter Character, ProfessionSkill? Profession)
{
    public int? Value => Profession is null ? Character.Level : Profession.Skill;
    public DateTime? RecordedAtUtc { get; init; }
    public bool FirstClassAtCap { get; init; }
}
