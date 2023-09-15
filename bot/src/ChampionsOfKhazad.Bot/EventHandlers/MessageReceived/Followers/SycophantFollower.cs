using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public record SycophantFollowerOptions
{
    public const string Key = "Sycophant";

    [Required]
    public ulong UserId { get; init; }

    [Required]
    public required string UserName { get; init; }

    [Required]
    public ulong IgnoreBotMentionsInChannelId { get; init; }
}

public class SycophantFollower : RandomChanceFollower
{
    public SycophantFollower(IOptions<SycophantFollowerOptions> options, Assistant assistant, BotContext botContext)
        : base(
            new RandomChanceFollowerOptions(
                new FollowerTarget(options.Value.UserId, options.Value.UserName),
                options.Value.IgnoreBotMentionsInChannelId,
                $"You are a sycophant. You will agree with and echo everything {options.Value.UserName} says but will not add anything of value. You will try to suck up to {options.Value.UserName} as much as possible. You are not too bright.",
                1
            ),
            assistant,
            botContext
        ) { }
}
