using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot.EventLoop;

public class WordOfTheDayHintEventOptions
{
    public const string Key = "WordOfTheDayHint";

    [Required]
    public required ulong TextChannelId { get; set; }

    [Required]
    public required ushort MinimumTimeSinceLastWinnerMinutes { get; set; }

    [Required]
    public required ushort MeanTimeToHappenMinutes { get; set; }

    [Required]
    public required ushort CooldownMinutes { get; set; }
}
