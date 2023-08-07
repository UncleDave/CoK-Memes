using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public class MentionHandlerOptions
{
    public const string Key = "Mention";

    [Required]
    public required ulong ChannelId { get; init; }

    public ulong CringeAsideUserId { get; init; }
}
