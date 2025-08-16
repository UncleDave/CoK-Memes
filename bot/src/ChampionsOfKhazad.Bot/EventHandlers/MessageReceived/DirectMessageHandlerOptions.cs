using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public class DirectMessageHandlerOptions
{
    public const string Key = "DirectMessage";

    [Required]
    public required ulong AdminUserId { get; init; }
}
