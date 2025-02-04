using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public record NoNutNovemberExpertFollowerOptions : BaseFollowerOptions
{
    public const string Key = "NoNutNovemberExpert";
}

public class NoNutNovemberExpertFollower(
    IOptions<AllFollowersOptions> allFollowersOptions,
    IOptions<NoNutNovemberExpertFollowerOptions> options,
    BotContext botContext,
    ILogger<RandomChanceFollowerTriggerStrategy> triggerStrategyLogger,
    ICompletionService completionService
)
    : StrategyFollower(
        allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
        new AllOfFollowerTriggerStrategy(
            new TargetUserFollowerTriggerStrategy(options.Value.UserId),
            new RandomChanceFollowerTriggerStrategy(options.Value.Chance, triggerStrategyLogger),
            new TriggerWordFollowerTriggerStrategy("nnn", "no nut november")
        ),
        new PersonalityFollowerResponseStrategy(completionService.NoNutNovemberExpert, botContext.BotId),
        botContext
    );
