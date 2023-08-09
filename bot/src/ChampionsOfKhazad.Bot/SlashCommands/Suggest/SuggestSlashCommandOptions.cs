using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public class SuggestSlashCommandOptions
{
    public const string Key = "suggest";

    [Required]
    public ulong UserId { get; init; }
}
