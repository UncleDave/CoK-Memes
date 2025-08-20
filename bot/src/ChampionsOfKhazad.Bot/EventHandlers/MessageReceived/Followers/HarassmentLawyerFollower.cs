using System.ComponentModel.DataAnnotations;
using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public record HarassmentLawyerFollowerOptions : BaseFollowerOptions
{
    public const string Key = "HarassmentLawyer";

    [Required]
    public ulong ClientUserId { get; init; }

    [Required]
    public required string ClientUserName { get; init; }
}

public class HarassmentLawyerFollower(
    IOptions<AllFollowersOptions> allFollowersOptions,
    IOptions<HarassmentLawyerFollowerOptions> options,
    BotContext botContext,
    ILogger<RandomChanceFollowerTriggerStrategy> triggerStrategyLogger,
    ICompletionService completionService
)
    : StrategyFollower(
        allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
        new AllOfFollowerTriggerStrategy(
            new TargetUserFollowerTriggerStrategy(options.Value.UserId),
            new RandomChanceFollowerTriggerStrategy(options.Value.Chance, triggerStrategyLogger),
            new MentionFollowerTriggerStrategy(options.Value.ClientUserId)
        ),
        // new SplitPersonalityFollowerResponseStrategy(
        //     [completionService.HarassmentLawyer, completionService.ProHarassmentLawyer],
        //     botContext.BotId,
        //     new Dictionary<string, object?> { ["clientName"] = options.Value.ClientUserName }
        // ),
        new PersonalityFollowerResponseStrategy(
            completionService.HarassmentLawyer,
            botContext.BotId,
            new Dictionary<string, object?> { ["clientName"] = options.Value.ClientUserName }
        ),
        botContext
    );
