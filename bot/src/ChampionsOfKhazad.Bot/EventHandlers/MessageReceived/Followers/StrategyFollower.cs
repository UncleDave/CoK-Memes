namespace ChampionsOfKhazad.Bot;

public abstract class StrategyFollower(
    ulong ignoreBotMentionsInChannelId,
    IFollowerTriggerStrategy triggerStrategy,
    IFollowerResponseStrategy responseStrategy,
    BotContext botContext
) : Follower(ignoreBotMentionsInChannelId, botContext)
{
    protected override Task<bool> ShouldTrigger(MessageReceived notification) => Task.FromResult(triggerStrategy.ShouldTrigger(notification));

    protected override Task<string> GetResponseAsync(MessageReceived notification, CancellationToken cancellationToken = default) =>
        responseStrategy.GetResponseAsync(notification, cancellationToken);
}
