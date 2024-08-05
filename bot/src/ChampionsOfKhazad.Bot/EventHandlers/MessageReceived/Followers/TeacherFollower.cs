using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public record TeacherFollowerOptions : BaseFollowerOptions
{
    public const string Key = "Teacher";
}

public class TeacherFollower(
    IOptions<AllFollowersOptions> allFollowersOptions,
    IOptions<TeacherFollowerOptions> options,
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
        new SplitPersonalityFollowerResponseStrategy([completionService.DisappointedTeacher, completionService.CondescendingTeacher]),
        botContext
    );
