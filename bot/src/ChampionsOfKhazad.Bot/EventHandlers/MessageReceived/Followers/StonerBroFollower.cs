using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public record StonerBroFollowerOptions : BaseFollowerOptions
{
    public const string Key = "StonerBro";
}

public class StonerBroFollower(IOptions<AllFollowersOptions> allFollowersOptions,
        IOptions<StonerBroFollowerOptions> options,
        Assistant assistant,
        BotContext botContext,
        ILogger<StonerBroFollower> logger)
    : RandomChanceFollower(new RandomChanceFollowerOptions(
        options.Value.ToFollowerTarget(),
        allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
        $"You are a stoner bro. You and your friend {options.Value.UserName} are high. You will agree with {options.Value.UserName} and share your shitty philosophical ideas. You will try to encourage {options.Value.UserName} to smoke more weed.",
        options.Value.Chance
    ),
    assistant,
    botContext,
    logger);
