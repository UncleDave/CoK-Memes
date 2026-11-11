using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public sealed class SpecialsOptions
{
    public const string Key = "Specials";

    public ulong DestinationChannelId { get; set; }

    [Required]
    public string DestinationChannelName { get; set; } = "ai-tavern";

    public ulong[] SourceChannelIds { get; set; } = [];
}
