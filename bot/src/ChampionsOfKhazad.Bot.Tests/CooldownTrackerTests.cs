namespace ChampionsOfKhazad.Bot.Tests;

public class CooldownTrackerTests
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(5);

    [Fact]
    public void CooldownExpiresAtTheExactDeadline()
    {
        var clock = new TestClock();
        var tracker = new CooldownTracker<string>(clock);

        Assert.True(tracker.TryAcquire("feature", Cooldown));
        clock.Advance(Cooldown - TimeSpan.FromTicks(1));
        Assert.True(tracker.IsOnCooldown("feature", Cooldown));
        Assert.False(tracker.TryAcquire("feature", Cooldown));
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.False(tracker.IsOnCooldown("feature", Cooldown));
        Assert.True(tracker.TryAcquire("feature", Cooldown));
    }

    [Fact]
    public void RejectedAttemptsCanPreserveTheDirectMessageInactivityWindow()
    {
        var clock = new TestClock();
        var tracker = new CooldownTracker<ulong>(clock);

        Assert.True(tracker.TryAcquire(42, Cooldown, refreshOnRejection: true));
        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.False(tracker.TryAcquire(42, Cooldown, refreshOnRejection: true));
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(tracker.IsOnCooldown(42, Cooldown));
        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.True(tracker.TryAcquire(42, Cooldown, refreshOnRejection: true));
    }

    [Fact]
    public void CheckingEligibilityDoesNotAcquireTheCooldown()
    {
        var tracker = new CooldownTracker<string>(new TestClock());

        Assert.False(tracker.IsOnCooldown("feature", Cooldown));
        Assert.False(tracker.IsOnCooldown("feature", Cooldown));
        Assert.True(tracker.TryAcquire("feature", Cooldown));
        Assert.True(tracker.TryAcquire("another-feature", Cooldown));
    }

    [Fact]
    public async Task OnlyOneConcurrentRequestAcquiresTheSameCooldown()
    {
        var tracker = new CooldownTracker<string>(new TestClock());
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable
            .Range(0, 64)
            .Select(_ =>
                Task.Run(async () =>
                {
                    await start.Task.WaitAsync(TestContext.Current.CancellationToken);
                    return tracker.TryAcquire("feature", Cooldown);
                })
            )
            .ToArray();

        start.SetResult();
        var results = await Task.WhenAll(requests);

        Assert.Single(results, acquired => acquired);
    }

    [Fact]
    public void FollowerInstancesShareTheInjectedCooldownOwner()
    {
        var tracker = new CooldownTracker<string>(new TestClock());
        var first = new CooldownFollowerTriggerStrategy("feature", Cooldown, tracker);
        var second = new CooldownFollowerTriggerStrategy("feature", Cooldown, tracker);

        Assert.True(first.ShouldTrigger(null!));
        Assert.False(second.ShouldTrigger(null!));
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan time) => _now += time;
    }
}
