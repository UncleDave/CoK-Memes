using Discord;

namespace ChampionsOfKhazad.Bot;

public record RandomChanceTriggerWordFollowerOptions(
    FollowerTarget Target,
    ulong IgnoreBotMentionsInChannelId,
    string Instructions,
    ushort Chance,
    string TriggerWord
) : RandomChanceFollowerOptions(Target, IgnoreBotMentionsInChannelId, Instructions, Chance);

public abstract class RandomChanceTriggerWordFollower : RandomChanceFollower
{
    private readonly RandomChanceTriggerWordFollowerOptions _options;

    protected RandomChanceTriggerWordFollower(RandomChanceTriggerWordFollowerOptions options, Assistant assistant, BotContext botContext)
        : base(options, assistant, botContext)
    {
        _options = options;
    }

    protected override bool ShouldTrigger(IUserMessage message) =>
        message.Content.ToLowerInvariant().Contains(_options.TriggerWord) && base.ShouldTrigger(message);
}
