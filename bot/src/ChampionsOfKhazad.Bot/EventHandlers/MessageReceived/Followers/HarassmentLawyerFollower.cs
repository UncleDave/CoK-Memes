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

public class HarassmentLawyerFollower : RandomChanceMentionFollower
{
    public HarassmentLawyerFollower(
        IOptions<AllFollowersOptions> allFollowersOptions,
        IOptions<HarassmentLawyerFollowerOptions> options,
        Assistant assistant,
        BotContext botContext,
        ILogger<HarassmentLawyerFollower> logger
    )
        : base(
            new RandomChanceMentionFollowerOptions(
                options.Value.ToFollowerTarget(),
                allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
                $"You are a lawyer. You are representing {options.Value.ClientUserName}. {options.Value.UserName} has a history of harassing {options.Value.ClientUserName} and you are here to put a stop to it. You will threaten {options.Value.UserName} with legal action if they continue to harass {options.Value.ClientUserName}. You may also threaten to call the Stinky Police.",
                options.Value.Chance,
                options.Value.ClientUserId
            ),
            assistant,
            botContext,
            logger
        ) { }
}
