using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot.EventLoop;

public class ExcludedFromVoiceEventOptions
{
    public const string Key = "ExcludedFromVoice";

    [Required]
    public required ulong TextChannelId { get; set; }

    [Required]
    public required ushort MinimumOccupants { get; set; }

    [Required]
    public required ushort MeanTimeToHappenMinutes { get; set; }

    [Required]
    public required ushort CooldownMinutes { get; set; }
}
