using ChampionsOfKhazad.Bot.GenAi;
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
    BotContext botContext,
    ILogger<RandomChanceFollowerTriggerStrategy> triggerStrategyLogger,
    ICompletionService completionService
)
    : StrategyFollower(
        allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
        new AllOfFollowerTriggerStrategy(
            new TargetUserFollowerTriggerStrategy(options.Value.UserId),
            new RandomChanceFollowerTriggerStrategy(options.Value.Chance, triggerStrategyLogger)
        ),
        new PersonalityFollowerResponseStrategy(completionService.StonerBro, botContext.BotId),
        botContext
    );
