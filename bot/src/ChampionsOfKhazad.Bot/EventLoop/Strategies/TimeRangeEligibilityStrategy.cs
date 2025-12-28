namespace ChampionsOfKhazad.Bot.EventLoop;

public class TimeRangeEligibilityStrategy(TimeOnly start, TimeOnly end) : IEligibilityStrategy
{
    public Task<bool> IsEligibleToFireAsync(CancellationToken cancellationToken)
    {
        var now = TimeOnly.FromDateTime(DateTime.Now);

        bool isEligible;

        if (start <= end)
        {
            // Same day
            isEligible = now >= start && now <= end;
        }
        else
        {
            // Crosses midnight
            isEligible = now >= start || now <= end;
        }

        return Task.FromResult(isEligible);
    }
}
