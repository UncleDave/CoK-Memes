using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public record GermanyBisFollowerOptions
{
    public const string Key = "GermanyBis";

    [Required]
    public ulong UserId { get; init; }
}

public class GermanyBisFollower(IOptions<AllFollowersOptions> allFollowersOptions, IOptions<GermanyBisFollowerOptions> options, BotContext botContext)
    : StrategyFollower(
        allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
        new AllOfFollowerTriggerStrategy(
            new TargetUserFollowerTriggerStrategy(options.Value.UserId, 198167155219365888),
            new TriggerWordFollowerTriggerStrategy("germany bis"),
            new CooldownFollowerTriggerStrategy("GermanyBis", TimeSpan.FromHours(1))
        ),
        new StaticFollowerResponseStrategy("https://i.imgur.com/6OklIvu.png"),
        botContext
    );
