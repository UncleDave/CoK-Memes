using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public record RaidsSlashCommandRaid(ulong ChannelId, DayOfWeek DayOfWeek);

public class RaidsSlashCommandOptions
{
    public const string Key = "Raids";

    [Required]
    public required IEnumerable<RaidsSlashCommandRaid> Raids { get; init; }

    public IEnumerable<string> Mentions { get; init; } = [];
}
