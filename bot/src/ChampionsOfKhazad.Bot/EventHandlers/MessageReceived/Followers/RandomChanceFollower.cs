using Discord;

namespace ChampionsOfKhazad.Bot;

public record RandomChanceFollowerOptions(FollowerTarget Target, ulong IgnoreBotMentionsInChannelId, string Instructions, ushort Chance)
    : FollowerOptions(Target, IgnoreBotMentionsInChannelId, Instructions);

public abstract class RandomChanceFollower : Follower
{
    private readonly RandomChanceFollowerOptions _options;

    protected RandomChanceFollower(RandomChanceFollowerOptions options, Assistant assistant, BotContext botContext)
        : base(options, assistant, botContext)
    {
        _options = options;
    }

    protected override bool ShouldTrigger(IUserMessage message) => RandomUtils.Roll(_options.Chance);
}
