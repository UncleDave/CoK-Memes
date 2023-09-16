using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public record AllFollowersOptions
{
    public const string Key = "Followers";

    [Required]
    public ulong IgnoreBotMentionsInChannelId { get; init; }
}
