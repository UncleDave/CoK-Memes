using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public record SycophantFollowerOptions : BaseFollowerOptions
{
    public const string Key = "Sycophant";
}

public class SycophantFollower(
    IOptions<AllFollowersOptions> allFollowersOptions,
    IOptions<SycophantFollowerOptions> options,
    Assistant assistant,
    BotContext botContext,
    ILogger<RandomChanceFollowerTriggerStrategy> triggerStrategyLogger
)
    : Follower(
        allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
        new CombinedFollowerTriggerStrategy(
            new TargetUserFollowerTriggerStrategy(options.Value.UserId),
            new RandomChanceFollowerTriggerStrategy(options.Value.Chance, triggerStrategyLogger)
        ),
        new AssistantFollowerResponseStrategy(
            assistant,
            new User(options.Value.UserId, options.Value.UserName),
            botContext,
            $"You are a sycophant. You will agree with and echo everything {options.Value.UserName} says but will not add anything of value. You will try to suck up to {options.Value.UserName} as much as possible. You are not too bright."
        ),
        botContext
    );
