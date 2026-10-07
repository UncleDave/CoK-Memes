using ChampionsOfKhazad.Bot.EventLoop;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.Tests;

public class EventLoopServiceTests
{
    [Fact]
    public async Task UnreadyTickDoesNotResolveContextDependentEventsAndLaterReadyTickFires()
    {
        var resolved = 0;
        var fired = 0;
        var services = new ServiceCollection();
        services.AddScoped<IEventLoopEvent>(_ =>
        {
            resolved++;
            return new TestEvent(() =>
            {
                fired++;
                return Task.CompletedTask;
            });
        });
        await using var provider = services.BuildServiceProvider();
        var context = new BotContextProvider();
        var loop = CreateLoop(provider, context);
        await loop.FireEventsAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        Assert.Equal(0, resolved);
        context.IsReady = true;
        await loop.FireEventsAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        Assert.Equal(1, resolved);
        Assert.Equal(1, fired);
        context.IsReady = false;
        await loop.FireEventsAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        Assert.Equal(1, resolved);
    }

    [Fact]
    public async Task SessionResumeWithoutReadyRestoresGuildContextAndScheduledEvents()
    {
        await using var client = new DiscordSocketClient();
        var original = Guild(1);
        var available = Guild(1);
        var context = new BotContextProvider { BotContext = new BotContext(42, original, client), IsReady = true };
        var bot = CreateBot(client, context);
        var fired = 0;
        var services = new ServiceCollection();
        services.AddScoped<IEventLoopEvent>(_ => new TestEvent(() =>
        {
            fired++;
            return Task.CompletedTask;
        }));
        await using var provider = services.BuildServiceProvider();
        var loop = CreateLoop(provider, context);
        var token = TestContext.Current.CancellationToken;
        await loop.FireEventsAsync(TimeSpan.FromMinutes(1), token);
        await bot.DisconnectedAsync(new IOException("Gateway disconnected"));
        await loop.FireEventsAsync(TimeSpan.FromMinutes(1), token);
        Assert.Equal(1, fired);
        await bot.RestoreReadinessAsync(available, connectionUsable: false);
        Assert.False(context.IsReady);
        await bot.RestoreReadinessAsync(available, connectionUsable: true);
        Assert.True(context.IsReady);
        Assert.Same(available, context.BotContext!.Guild);
        Assert.Equal(42UL, context.BotContext.BotId);
        Assert.Null(client.CurrentUser);
        await loop.FireEventsAsync(TimeSpan.FromMinutes(1), token);
        Assert.Equal(2, fired);
    }

    [Theory]
    [InlineData("initial startup")]
    [InlineData("different guild")]
    [InlineData("unusable connection")]
    public async Task RecoverySignalsDoNotMakeIncompleteOrForeignGuildContextsReady(string state)
    {
        await using var client = new DiscordSocketClient();
        var context = new BotContextProvider { BotContext = state == "initial startup" ? null : new BotContext(42, Guild(1), client) };
        var bot = CreateBot(client, context);
        await bot.RestoreReadinessAsync(Guild(state == "different guild" ? 2UL : 1UL), state != "unusable connection");
        Assert.False(context.IsReady);
    }

    [Fact]
    public async Task EventScopeLivesThroughFiringAndIsDisposedAsynchronously()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resource = new AsyncResource();
        var services = new ServiceCollection();
        services.AddScoped(_ => resource);
        services.AddScoped<IEventLoopEvent>(provider => new TestEvent(async () =>
        {
            var scoped = provider.GetRequiredService<AsyncResource>();
            entered.SetResult();
            await release.Task;
            Assert.False(scoped.Disposed);
        }));
        await using var provider = services.BuildServiceProvider();
        var loop = CreateLoop(provider, new BotContextProvider { IsReady = true });
        var firing = loop.FireEventsAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        await entered.Task;
        Assert.False(resource.Disposed);
        release.SetResult();
        await firing;
        Assert.True(resource.Disposed);
    }

    [Fact]
    public async Task FailedEventDoesNotPreventFollowingEventFromFiring()
    {
        var fired = false;
        var services = new ServiceCollection();
        services.AddScoped<IEventLoopEvent>(_ => new TestEvent(() => throw new InvalidOperationException("Failed event")));
        services.AddScoped<IEventLoopEvent>(_ => new TestEvent(() =>
        {
            fired = true;
            return Task.CompletedTask;
        }));
        await using var provider = services.BuildServiceProvider();
        await CreateLoop(provider, new BotContextProvider { IsReady = true })
            .FireEventsAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        Assert.True(fired);
    }

    [Fact]
    public async Task ShutdownCancellationPropagatesWithoutRunningFollowingEvents()
    {
        using var cancellation = new CancellationTokenSource();
        var followingFired = false;
        var services = new ServiceCollection();
        services.AddScoped<IEventLoopEvent>(_ => new TestEvent(() =>
        {
            cancellation.Cancel();
            return Task.FromCanceled(cancellation.Token);
        }));
        services.AddScoped<IEventLoopEvent>(_ => new TestEvent(() =>
        {
            followingFired = true;
            return Task.CompletedTask;
        }));
        await using var provider = services.BuildServiceProvider();
        var loop = CreateLoop(provider, new BotContextProvider { IsReady = true });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loop.FireEventsAsync(TimeSpan.FromMinutes(1), cancellation.Token));
        Assert.False(followingFired);
    }

    [Fact]
    public async Task ObservationEventRequiresReadinessAndCompletesAnEmptyScanWithoutDiscovery()
    {
        var store = new MemoryStore();
        var reader = new EmptyReader();
        var options = Options.Create(new NotebookObserverOptions());
        var notebook = new NotebookService(store, null!, null!, null!, TimeProvider.System, NullLogger<NotebookService>.Instance, reader, options);
        var observer = new NotebookObserverService(
            store,
            notebook,
            reader,
            null!,
            options,
            TimeProvider.System,
            NullLogger<NotebookObserverService>.Instance
        );
        var context = new BotContextProvider();
        var observation = new NotebookObservationEvent(observer, context, options);
        var token = TestContext.Current.CancellationToken;
        Assert.False(await observation.EligibleToFire(token));
        await observation.FireAsync(token);
        Assert.Equal(0, reader.Reads);
        context.IsReady = true;
        Assert.True(await observation.EligibleToFire(token));
        Assert.Equal(0, reader.Reads);
        await observation.FireAsync(token);
        Assert.Equal(1, reader.Reads);
        Assert.Equal(NotebookObservationOutcome.NoMessages, Assert.Single(store.State.Observer.Scans).Outcome);
        Assert.Empty(store.State.Observer.DiscoveryAttempts);
        Assert.False(await observation.EligibleToFire(token));
    }

    private static EventLoopService CreateLoop(IServiceProvider provider, BotContextProvider context) =>
        new(
            Options.Create(new EventLoopOptions { IntervalMinutes = 1 }),
            provider,
            NullLogger<EventLoopService>.Instance,
            context,
            TimeProvider.System
        );

    private static BotService CreateBot(DiscordSocketClient client, BotContextProvider context) =>
        new(
            client,
            NullLogger<BotService>.Instance,
            Options.Create(new BotOptions { Token = "unused-test-token", GuildId = 1 }),
            new NotificationQueue(Options.Create(new NotificationQueueOptions()), NullLogger<NotificationQueue>.Instance),
            context
        );

    private static IGuild Guild(ulong id) =>
        DiscordConversationFixture.Stub<IGuild>((method, _) => method.Name == "get_Id" ? id : throw new NotSupportedException(method.Name));

    private sealed class TestEvent(Func<Task> fire) : IEventLoopEvent
    {
        public string Name => "test";
        public TimeSpan MeanTimeToHappen => TimeSpan.FromMinutes(1);

        public Task<bool> EligibleToFire(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task FireAsync(CancellationToken cancellationToken) => fire();
    }

    private sealed class AsyncResource : IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MemoryStore : INotebookStore
    {
        public NotebookState State { get; private set; } = new();

        public Task<NotebookState> GetAsync(CancellationToken cancellationToken) => Task.FromResult(State);

        public Task<bool> TrySaveAsync(NotebookState state, CancellationToken cancellationToken)
        {
            if (state.Revision != State.Revision)
                return Task.FromResult(false);
            State = state with { Revision = state.Revision + 1 };
            return Task.FromResult(true);
        }
    }

    private sealed class EmptyReader : INotebookBackgroundSourceReader
    {
        public int Reads { get; private set; }

        public IReadOnlyList<ulong> GetChannelIds() => [1];

        public Task<NotebookObservationBatch?> ReadBatchAsync(ulong channelId, ulong afterMessageId, int limit, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult<NotebookObservationBatch?>(new(channelId, afterMessageId, []));
        }

        public Task<IReadOnlyList<NotebookSource>?> ReadSourcesAsync(IReadOnlyList<string> urls, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No notes expected");
    }
}
