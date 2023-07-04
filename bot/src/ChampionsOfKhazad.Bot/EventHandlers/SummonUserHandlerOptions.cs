using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public class SummonUserHandlerOptions
{
    public const string Key = "SummonUser";

    [Required]
    public ulong UserId { get; init; }

    [Required]
    public ulong LeaderId { get; init; }

    public bool AllowSingleUserStreaks { get; init; }
}
