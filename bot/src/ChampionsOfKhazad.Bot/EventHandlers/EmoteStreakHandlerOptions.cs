using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public class EmoteStreakHandlerOptions
{
    public const string Key = "EmoteStreak";

    [Required]
    public required string EmoteName { get; init; }

    [Required]
    public required ulong ChannelId { get; init; }

    public bool AllowSingleUserStreaks { get; init; }
}
