using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot.GenAi;

public class NotebookObserverOptions
{
    public const string Key = "NotebookObservation";

    public bool Enabled { get; init; } = true;

    [Range(1, 1440)]
    public int MeanTimeToHappenMinutes { get; init; } = 60;

    [Range(1, 1440)]
    public int CooldownMinutes { get; init; } = 30;

    [Range(1, 60)]
    public int LookbackMinutes { get; init; } = 60;

    [Range(1, 100)]
    public int DailyDiscoveryLimit { get; init; } = 12;

    [Range(1, NotebookService.DailyGuildEvaluationLimit)]
    public int DailyReviewLimit { get; init; } = 10;

    [Range(1, NotebookService.DailyGuildLimit)]
    public int DailyNoteLimit { get; init; } = 5;

    [Range(1, 20)]
    public int MaximumChannelsPerScan { get; init; } = 4;

    [Range(1, 100)]
    public int MaximumMessagesPerChannel { get; init; } = 20;

    [Range(4000, 100000)]
    public int MaximumInputCharacters { get; init; } = 20000;

    [Range(1, 600)]
    public int ScanTimeoutSeconds { get; init; } = 180;
}
