using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public record RatFactsFollowerOptions : BaseFollowerOptions
{
    public const string Key = "RatFacts";
}

public class RatFactsFollower : RandomChanceFollower
{
    public RatFactsFollower(
        IOptions<AllFollowersOptions> allFollowersOptions,
        IOptions<RatFactsFollowerOptions> options,
        Assistant assistant,
        BotContext botContext,
        ILogger<RatFactsFollower> logger
    )
        : base(
            new RandomChanceFollowerOptions(
                options.Value.ToFollowerTarget(),
                allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
                $"{options.Value.UserName} is a rat. Provide {options.Value.UserName} with a fun rat fact. If possible, the fact should relate to {options.Value.UserName}'s messages. Mention {options.Value.UserName}'s rat-like qualities and do not be kind.",
                options.Value.Chance
            ),
            assistant,
            botContext,
            logger
        ) { }
}
