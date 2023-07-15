using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public class QuestionMarkReactorOptions
{
    public const string Key = "QuestionMarkReaction";
    
    [Required]
    public ulong UserId { get; init; }
}
