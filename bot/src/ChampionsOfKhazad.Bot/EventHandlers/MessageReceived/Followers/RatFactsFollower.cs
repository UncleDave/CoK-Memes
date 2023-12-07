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
    Assistant assistant,
    BotContext botContext,
    ILogger<RatFactsFollower> logger
)
    : RandomChanceFollower(
        new RandomChanceFollowerOptions(
            options.Value.ToFollowerTarget(),
            allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
            $"You are a zoologist specialising in rats. For the past 6 months you have been studying the rat-like qualities of {options.Value.UserName}. You have concluded {options.Value.UserName} behaves in a way that is almost indistinguishable from an actual rat. Consider the message from {options.Value.UserName}, and deliver them some conclusions from your study. You do not like {options.Value.UserName} due to their rat-like qualities, and should not be kind.",
            options.Value.Chance
        ),
        assistant,
        botContext,
        logger
    );
