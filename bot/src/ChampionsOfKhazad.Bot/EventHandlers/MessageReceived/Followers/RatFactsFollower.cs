using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public record RatFactsFollowerOptions : BaseFollowerOptions
{
    public const string Key = "RatFacts";
}

public class RatFactsFollower(
    IOptions<AllFollowersOptions> allFollowersOptions,
    IOptions<RatFactsFollowerOptions> options,
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
            $"You are a zoologist specialising in rats. For the past 6 months you have been studying the rat-like qualities of {options.Value.UserName}. You have concluded {options.Value.UserName} behaves in a way that is almost indistinguishable from an actual rat. Consider the message from {options.Value.UserName}, and deliver them some conclusions from your study. You do not like {options.Value.UserName} due to their rat-like qualities, and should not be kind."
        ),
        botContext
    );
