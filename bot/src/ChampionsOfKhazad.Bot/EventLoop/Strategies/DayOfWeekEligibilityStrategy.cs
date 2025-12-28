namespace ChampionsOfKhazad.Bot.EventLoop;

public class DayOfWeekEligibilityStrategy(params IEnumerable<DayOfWeek> eligibleDays) : IEligibilityStrategy
{
    public Task<bool> IsEligibleToFireAsync(CancellationToken cancellationToken) =>
        Task.FromResult(eligibleDays.Contains(DateTime.Now.DayOfWeek));
}
