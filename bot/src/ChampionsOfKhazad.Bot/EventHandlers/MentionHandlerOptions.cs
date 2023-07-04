using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

// TODO: Generify channel-specific handlers

public class MentionHandlerOptions
{
    public const string Key = "Mention";

    [Required]
    public required ulong ChannelId { get; init; }

    public IReadOnlyDictionary<ulong, ushort>? HourlyUserQuotas { get; init; }
}
