using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public class ClownReactorOptions
{
    public const string Key = "ClownReaction";

    [Required]
    public ulong UserId { get; init; }
}
