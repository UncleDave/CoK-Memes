using MediatR;

namespace ChampionsOfKhazad.Bot.DiscordStats.StreakBreaks;

internal class StreakBrokenHandler : INotificationHandler<StreakBroken>
{
    private readonly IStoreStreakBreaks _streakBreakStore;

    public StreakBrokenHandler(IStoreStreakBreaks streakBreakStore)
    {
        _streakBreakStore = streakBreakStore;
    }

    public Task Handle(StreakBroken notification, CancellationToken cancellationToken)
    {
        var streakBreak = new StreakBreak(notification.UserId, notification.EmoteName, notification.Timestamp);
        return _streakBreakStore.InsertStreakBreakAsync(streakBreak);
    }
}
