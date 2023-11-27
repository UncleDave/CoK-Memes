using Discord;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

public record RandomChanceFollowerOptions(FollowerTarget Target, ulong IgnoreBotMentionsInChannelId, string Instructions, ushort Chance)
    : FollowerOptions(Target, IgnoreBotMentionsInChannelId, Instructions);

public abstract class RandomChanceFollower(RandomChanceFollowerOptions options,
        Assistant assistant,
        BotContext botContext,
        ILogger<RandomChanceFollower> logger)
    : Follower(options, assistant, botContext)
{
    protected override bool ShouldTrigger(IUserMessage message)
    {
        var roll = RandomUtils.Roll(options.Chance);

        logger.LogInformation(
            "Follower rolling for message from {Author}: {Roll} - {Result}",
            message.Author.GlobalName ?? message.Author.Username,
            roll.Roll,
            roll.Success ? "Success" : "Failure"
        );

        return roll.Success;
    }
}
