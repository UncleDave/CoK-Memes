namespace ChampionsOfKhazad.Bot.Roster;

public class GuildRosterOptions
{
    public const string Key = "GuildRoster";
    public bool Enabled { get; set; }
    public ulong GuildId { get; set; }
    public int RefreshSeconds { get; set; } = 60;
    public int ProfessionRefreshSeconds { get; set; } = 600;
    public ulong? MilestoneChannelId { get; set; }
    public int? LevelCap { get; set; } = 60;
    public int[] ProfessionMilestones { get; set; } = [75, 150, 225, 300];

    public void Validate()
    {
        if (
            RefreshSeconds < 10
            || ProfessionRefreshSeconds < 10
            || LevelCap is <= 0
            || ProfessionMilestones is null
            || ProfessionMilestones.Any(value => value <= 0)
            || (Enabled && GuildId == 0)
        )
            throw new ArgumentException("Invalid guild roster configuration.");
    }
}
