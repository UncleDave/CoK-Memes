using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public record SycophantFollowerOptions : BaseFollowerOptions
{
    public const string Key = "Sycophant";
}

public class SycophantFollower(IOptions<AllFollowersOptions> allFollowersOptions,
        IOptions<SycophantFollowerOptions> options,
        Assistant assistant,
        BotContext botContext,
        ILogger<SycophantFollower> logger)
    : RandomChanceFollower(new RandomChanceFollowerOptions(
        options.Value.ToFollowerTarget(),
        allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
        $"You are a sycophant. You will agree with and echo everything {options.Value.UserName} says but will not add anything of value. You will try to suck up to {options.Value.UserName} as much as possible. You are not too bright.",
        options.Value.Chance
    ),
    assistant,
    botContext,
    logger);
