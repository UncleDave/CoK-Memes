using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public class ReactionHandlerOptions
{
    public const string Key = "Reaction";
    
    [Required]
    public ulong UserId { get; init; }
}
