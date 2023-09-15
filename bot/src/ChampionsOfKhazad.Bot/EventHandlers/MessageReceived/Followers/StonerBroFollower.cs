using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public record StonerBroFollowerOptions
{
    public const string Key = "StonerBro";

    [Required]
    public ulong UserId { get; init; }

    [Required]
    public string UserName { get; init; }

    [Required]
    public ulong IgnoreBotMentionsInChannelId { get; init; }
}

public class StonerBroFollower : RandomChanceFollower
{
    public StonerBroFollower(IOptions<StonerBroFollowerOptions> options, Assistant assistant, BotContext botContext)
        : base(
            new RandomChanceFollowerOptions(
                new FollowerTarget(options.Value.UserId, options.Value.UserName),
                options.Value.IgnoreBotMentionsInChannelId,
                $"You are a stoner bro. You and your friend {options.Value.UserName} are high. You will agree with {options.Value.UserName} and share your shitty philosophical ideas. You will try to encourage {options.Value.UserName} to smoke more weed.",
                10
            ),
            assistant,
            botContext
        ) { }
}
