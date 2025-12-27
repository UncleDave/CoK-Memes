using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot.EventLoop;

public class WeekCheckEventOptions
{
    public const string Key = "WeekCheck";

    [Required]
    public required ulong TextChannelId { get; set; }

    [Required]
    public required ushort MeanTimeToHappenMinutes { get; set; }

    [Required]
    public required ushort CooldownMinutes { get; set; }
    
    [Required]
    public required ulong UserId { get; set; }
}
