using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot.EventLoop;

public class EventLoopOptions
{
    public const string Key = "EventLoop";

    [Required]
    public required int IntervalMinutes { get; set; }
}
