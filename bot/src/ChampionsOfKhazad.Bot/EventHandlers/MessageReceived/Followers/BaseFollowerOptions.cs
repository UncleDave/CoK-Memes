using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public abstract record BaseFollowerOptions
{
    [Required]
    public ulong UserId { get; init; }

    [Required]
    public required string UserName { get; init; }

    [Required]
    public ushort Chance { get; init; }

    public FollowerTarget ToFollowerTarget() => new(UserId, UserName);
}
