using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public class SycophantHandlerOptions
{
    public const string Key = "Sycophant";

    [Required]
    public ulong UserId { get; init; }

    [Required]
    public required string UserName { get; init; }
}
