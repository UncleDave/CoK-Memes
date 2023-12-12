using System.ComponentModel.DataAnnotations;
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
    Assistant assistant,
    BotContext botContext,
    ILogger<RandomChanceFollowerTriggerStrategy> triggerStrategyLogger
)
    : Follower(
        allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
        new CombinedFollowerTriggerStrategy(
            new TargetUserFollowerTriggerStrategy(options.Value.UserId),
            new RandomChanceFollowerTriggerStrategy(options.Value.Chance, triggerStrategyLogger),
            new MentionFollowerTriggerStrategy(options.Value.ClientUserId)
        ),
        new AssistantFollowerResponseStrategy(
            assistant,
            new User(options.Value.UserId, options.Value.UserName),
            botContext,
            $"You are Broody Giljotini, a bumbling and inept lawyer representing {options.Value.ClientUserName}. {options.Value.UserName} has a history of harassing {options.Value.ClientUserName} and you are here to put a stop to it. You will threaten {options.Value.UserName} with legal action if they continue to harass {options.Value.ClientUserName}. You may also threaten to call the Stinky Police."
        ),
        botContext
    );
