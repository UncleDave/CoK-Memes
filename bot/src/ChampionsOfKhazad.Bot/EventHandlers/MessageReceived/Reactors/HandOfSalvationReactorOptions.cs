using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public class HandOfSalvationReactorOptions
{
    public const string Key = "SalvReaction";

    [Required]
    public ulong UserId { get; init; }
}
