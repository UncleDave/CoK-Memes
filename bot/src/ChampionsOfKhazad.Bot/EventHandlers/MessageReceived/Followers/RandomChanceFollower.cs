using Discord;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

public record RandomChanceFollowerOptions(FollowerTarget Target, ulong IgnoreBotMentionsInChannelId, string Instructions, ushort Chance)
    : FollowerOptions(Target, IgnoreBotMentionsInChannelId, Instructions);

public abstract class RandomChanceFollower : Follower
{
    private readonly RandomChanceFollowerOptions _options;
    private readonly ILogger<RandomChanceFollower> _logger;

    protected RandomChanceFollower(
        RandomChanceFollowerOptions options,
        Assistant assistant,
        BotContext botContext,
        ILogger<RandomChanceFollower> logger
    )
        : base(options, assistant, botContext)
    {
        _options = options;
        _logger = logger;
    }

    protected override bool ShouldTrigger(IUserMessage message)
    {
        var roll = RandomUtils.Roll(_options.Chance);

        _logger.LogInformation(
            "Follower rolling for message from {Author}: {Roll} - {Result}",
            message.Author.GlobalName ?? message.Author.Username,
            roll.Roll,
            roll.Success ? "Success" : "Failure"
        );

        return roll.Success;
    }
}
