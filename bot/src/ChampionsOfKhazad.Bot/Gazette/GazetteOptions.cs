using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public sealed class GazetteOptions
{
    public const string Key = "Gazette";

    public ulong DestinationChannelId { get; set; }

    [Required]
    public string DestinationChannelName { get; set; } = "ai-tavern";

    public ulong[] SourceChannelIds { get; set; } = [];

    public bool IllustrationsEnabled { get; set; } = true;

    [Range(0, 10)]
    public int DailyIllustrationLimit { get; set; } = 3;
}
