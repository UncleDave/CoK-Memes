using ChampionsOfKhazad.Bot.GenAi;
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
    BotContext botContext,
    ILogger<RandomChanceFollowerTriggerStrategy> triggerStrategyLogger,
    ICompletionService completionService
)
    : Follower(
        allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
        new AllOfFollowerTriggerStrategy(
            new TargetUserFollowerTriggerStrategy(options.Value.UserId),
            new RandomChanceFollowerTriggerStrategy(options.Value.Chance, triggerStrategyLogger)
        ),
        new PersonalityFollowerResponseStrategy(completionService.RatExpert, botContext.BotId),
        botContext
    );
