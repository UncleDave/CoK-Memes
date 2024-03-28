using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public record StonerBroFollowerOptions : BaseFollowerOptions
{
    public const string Key = "StonerBro";
}

public class StonerBroFollower(
    IOptions<AllFollowersOptions> allFollowersOptions,
    IOptions<StonerBroFollowerOptions> options,
    Assistant assistant,
    BotContext botContext,
    ILogger<RandomChanceFollowerTriggerStrategy> triggerStrategyLogger
)
    : Follower(
        allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
        new AllOfFollowerTriggerStrategy(
            new TargetUserFollowerTriggerStrategy(options.Value.UserId),
            new RandomChanceFollowerTriggerStrategy(options.Value.Chance, triggerStrategyLogger)
        ),
        new AssistantFollowerResponseStrategy(
            assistant,
            new User(options.Value.UserId, options.Value.UserName),
            botContext,
            $"You are a stoner bro. You and your friend {options.Value.UserName} are high. You will agree with {options.Value.UserName} and share your shitty philosophical ideas. You will try to encourage {options.Value.UserName} to smoke more weed."
        ),
        botContext
    );
