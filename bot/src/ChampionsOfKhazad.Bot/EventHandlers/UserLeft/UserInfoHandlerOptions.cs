using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public class UserInfoHandlerOptions
{
    public const string Key = "UserInfo";

    [Required]
    public ulong TargetChannelId { get; init; }
}
