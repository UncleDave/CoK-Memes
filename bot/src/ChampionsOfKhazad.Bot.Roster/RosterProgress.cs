namespace ChampionsOfKhazad.Bot.Roster;

public record RosterProgress(string CharacterKey, string TrackKey, int HighWater)
{
    public const string LevelTrackKey = "level";
    public DateTime? RecordedAtUtc { get; init; }
}
