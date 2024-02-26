using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public record NumberwangFollowerOptions
{
    public const string Key = "Numberwang";

    [Required]
    public ushort Chance { get; init; }
}

public class NumberwangFollower(
    IOptions<AllFollowersOptions> allFollowersOptions,
    IOptions<NumberwangFollowerOptions> options,
    BotContext botContext,
    ILogger<RandomChanceFollowerTriggerStrategy> triggerStrategyLogger
)
    : Follower(
        allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
        new CombinedFollowerTriggerStrategy(
            new RandomChanceFollowerTriggerStrategy(options.Value.Chance, triggerStrategyLogger),
            new NoEmbedsFollowerTriggerStrategy(),
            new NumberFollowerTriggerStrategy(3)
        ),
        new StaticFollowerResponseStrategy("That's Numberwang!"),
        botContext
    );
