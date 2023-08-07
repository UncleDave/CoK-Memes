using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public class HallOfFameReactionHandlerOptions
{
    public const string Key = "HallOfFame";

    [Required]
    public required string EmoteName { get; init; }

    [Required]
    public ulong MessageAuthorId { get; init; }

    [Required]
    public ushort Threshold { get; init; }

    [Required]
    public ulong TargetChannelId { get; init; }
}
