using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public class NotificationQueueOptions
{
    public const string Key = "NotificationQueue";

    [Range(1, int.MaxValue)]
    public int Capacity { get; set; } = 100;

    [Range(1, int.MaxValue)]
    public int WorkerCount { get; set; } = 4;
}
