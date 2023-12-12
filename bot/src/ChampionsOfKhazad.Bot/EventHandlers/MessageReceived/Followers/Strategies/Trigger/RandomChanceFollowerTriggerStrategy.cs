using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

public class RandomChanceFollowerTriggerStrategy(ushort chance, ILogger<RandomChanceFollowerTriggerStrategy> logger) : IFollowerTriggerStrategy
{
    public bool ShouldTrigger(MessageReceived notification)
    {
        var roll = RandomUtils.Roll(chance);

        logger.LogInformation(
            "Follower rolling for message from {Author}: {Roll} - {Result}",
            notification.Message.Author.GlobalName ?? notification.Message.Author.Username,
            roll.Roll,
            roll.Success ? "Success" : "Failure"
        );

        return roll.Success;
    }
}
