using ChampionsOfKhazad.Bot.GenAi;
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
    ICompletionService completionService,
    BotContext botContext,
    ILogger<RandomChanceFollowerTriggerStrategy> triggerStrategyLogger
)
    : Follower(
        allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
        new AllOfFollowerTriggerStrategy(
            new TargetUserFollowerTriggerStrategy(options.Value.UserId),
            new RandomChanceFollowerTriggerStrategy(options.Value.Chance, triggerStrategyLogger)
        ),
        new SplitPersonalityFollowerResponseStrategy([completionService.Sycophant, completionService.Contrarian]),
        botContext
    );
