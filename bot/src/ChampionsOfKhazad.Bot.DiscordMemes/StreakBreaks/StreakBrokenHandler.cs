using MediatR;

namespace ChampionsOfKhazad.Bot.DiscordMemes.StreakBreaks;

internal class StreakBrokenHandler(IStoreStreakBreaks streakBreakStore) : INotificationHandler<StreakBroken>
{
    public Task Handle(StreakBroken notification, CancellationToken cancellationToken)
    {
        var streakBreak = new StreakBreak(notification.UserId, notification.EmoteName, notification.Timestamp);
        return streakBreakStore.InsertStreakBreakAsync(streakBreak);
    }
}
