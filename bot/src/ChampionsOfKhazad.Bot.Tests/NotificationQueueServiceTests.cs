using System.Collections.Concurrent;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.Tests;

public class NotificationQueueServiceTests
{
    [Fact]
    public async Task FullQueueRejectsImmediatelyWithoutDiscardingAcceptedWork()
    {
        await using var fixture = new QueueFixture(capacity: 1);
        Assert.True(fixture.Queue.TryEnqueue(new TestNotification(1)));
        Assert.False(fixture.Queue.TryEnqueue(new TestNotification(2)));

        await fixture.Service.StartAsync(TestContext.Current.CancellationToken);
        await fixture.StopAsync();

        Assert.Equal([1], fixture.State.Published.Select(x => x.NotificationId));
        Assert.Single(fixture.QueueLogger.Entries, x => x.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task EnqueueNeverRunsSynchronousHandlerWorkOnTheCallingThread()
    {
        await using var fixture = new QueueFixture(workerCount: 2);
        var started = NewSignal();
        var release = NewSignal();
        var otherWorkerFinished = NewSignal();
        fixture.State.OnPublish = (notification, cancellationToken) =>
        {
            if (notification.Id == 1)
            {
                started.TrySetResult();
                // Deliberately block before returning a Task to exercise synchronous handler code.
                release.Task.WaitAsync(cancellationToken).GetAwaiter().GetResult();
            }
            else
            {
                otherWorkerFinished.TrySetResult();
            }
            return Task.CompletedTask;
        };

        try
        {
            Assert.True(fixture.Queue.TryEnqueue(new TestNotification(1)));
            await fixture.Service.StartAsync(TestContext.Current.CancellationToken);
            await WaitAsync(started.Task);
            Assert.True(fixture.Queue.TryEnqueue(new TestNotification(2)));
            await WaitAsync(otherWorkerFinished.Task);
            Assert.False(release.Task.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
        }

        await fixture.StopAsync();
    }

    [Fact]
    public async Task WorkerCountBoundsConcurrencyAndEachNotificationOwnsItsScopeUntilCompletion()
    {
        await using var fixture = new QueueFixture(workerCount: 2);
        var bothWorkersStarted = NewSignal();
        var release = NewSignal();
        var active = 0;
        var activeCounts = new ConcurrentQueue<int>();
        fixture.State.OnPublish = async (_, cancellationToken) =>
        {
            var count = Interlocked.Increment(ref active);
            activeCounts.Enqueue(count);
            if (count == 2)
                bothWorkersStarted.TrySetResult();
            try
            {
                await release.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        };

        for (var i = 1; i <= 4; i++)
            Assert.True(fixture.Queue.TryEnqueue(new TestNotification(i)));

        try
        {
            await fixture.Service.StartAsync(TestContext.Current.CancellationToken);
            await WaitAsync(bothWorkersStarted.Task);
            Assert.Equal(2, fixture.State.Published.Count);
            Assert.Empty(fixture.State.DisposedScopes);
        }
        finally
        {
            release.TrySetResult();
        }

        await fixture.StopAsync();
        Assert.Equal(2, activeCounts.Max());
        Assert.Equal(4, fixture.State.Published.Count);
        Assert.Equal(4, fixture.State.Published.Select(x => x.ScopeId).Distinct().Count());
        Assert.Equal(fixture.State.Published.Select(x => x.ScopeId).Order(), fixture.State.DisposedScopes.Order());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerFailureIsLoggedAndDoesNotStopTheWorker(bool unrelatedCancellation)
    {
        await using var fixture = new QueueFixture();
        fixture.State.OnPublish = (notification, _) =>
        {
            if (notification.Id == 1)
                throw unrelatedCancellation ? new OperationCanceledException() : new InvalidOperationException("Handler failed");
            return Task.CompletedTask;
        };

        Assert.True(fixture.Queue.TryEnqueue(new TestNotification(1)));
        Assert.True(fixture.Queue.TryEnqueue(new TestNotification(2)));
        await fixture.Service.StartAsync(TestContext.Current.CancellationToken);
        await fixture.StopAsync();

        Assert.Equal([1, 2], fixture.State.Published.Select(x => x.NotificationId));
        Assert.Equal(2, fixture.State.DisposedScopes.Count);
        Assert.Single(fixture.ServiceLogger.Entries, x => x.Level == LogLevel.Error);
    }

    [Fact]
    public async Task ShutdownRejectsNewWorkAndDrainsAcceptedWorkWithoutCancellingIt()
    {
        await using var fixture = new QueueFixture();
        var started = NewSignal();
        var release = NewSignal();
        fixture.State.OnPublish = async (notification, cancellationToken) =>
        {
            if (notification.Id == 1)
            {
                started.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
        };

        Assert.True(fixture.Queue.TryEnqueue(new TestNotification(1)));
        Assert.True(fixture.Queue.TryEnqueue(new TestNotification(2)));
        await fixture.Service.StartAsync(TestContext.Current.CancellationToken);
        await WaitAsync(started.Task);

        Task stopping;
        try
        {
            stopping = fixture.Service.StopAsync(TestContext.Current.CancellationToken);
            Assert.False(stopping.IsCompleted);
            Assert.False(fixture.Queue.TryEnqueue(new TestNotification(3)));
        }
        finally
        {
            release.TrySetResult();
        }

        await WaitAsync(stopping);
        Assert.Equal([1, 2], fixture.State.Published.Select(x => x.NotificationId));
        Assert.Equal(2, fixture.State.DisposedScopes.Count);
        Assert.Empty(fixture.ServiceLogger.Entries);
    }

    [Fact]
    public async Task ShutdownDeadlineCancelsHandlersAndDoesNotStartPendingWork()
    {
        await using var fixture = new QueueFixture();
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.State.OnPublish = async (_, cancellationToken) =>
        {
            started.TrySetResult(cancellationToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        };

        Assert.True(fixture.Queue.TryEnqueue(new TestNotification(1)));
        Assert.True(fixture.Queue.TryEnqueue(new TestNotification(2)));
        await fixture.Service.StartAsync(TestContext.Current.CancellationToken);
        await WaitAsync(started.Task);
        var processingToken = await started.Task;
        using var deadline = new CancellationTokenSource();
        var stopping = fixture.Service.StopAsync(deadline.Token);
        Assert.False(processingToken.IsCancellationRequested);

        await deadline.CancelAsync();
        await WaitAsync(stopping);
        await WaitAsync(fixture.Service.ExecuteTask!);

        Assert.True(processingToken.IsCancellationRequested);
        Assert.Equal([1], fixture.State.Published.Select(x => x.NotificationId));
        Assert.Single(fixture.State.DisposedScopes);
        Assert.Single(fixture.ServiceLogger.Entries, x => x.Level == LogLevel.Warning);
        Assert.DoesNotContain(fixture.ServiceLogger.Entries, x => x.Level == LogLevel.Error);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void QueueLimitsMustBePositive(int capacity, int workerCount)
    {
        var services = new ServiceCollection();
        services
            .AddOptions<NotificationQueueOptions>()
            .Configure(options =>
            {
                options.Capacity = capacity;
                options.WorkerCount = workerCount;
            })
            .ValidateDataAnnotations();
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<NotificationQueueOptions>>().Value);
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static Task WaitAsync(Task task) => task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

    private sealed record TestNotification(int Id) : INotification;

    private sealed class PublisherState
    {
        public ConcurrentQueue<(int NotificationId, Guid ScopeId)> Published { get; } = new();
        public ConcurrentQueue<Guid> DisposedScopes { get; } = new();
        public Func<TestNotification, CancellationToken, Task> OnPublish { get; set; } = (_, _) => Task.CompletedTask;
    }

    private sealed class RecordingPublisher(PublisherState state) : IPublisher, IAsyncDisposable
    {
        private readonly Guid _scopeId = Guid.NewGuid();

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            var message = (TestNotification)notification;
            state.Published.Enqueue((message.Id, _scopeId));
            return state.OnPublish(message, cancellationToken);
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Publish((object)notification, cancellationToken);

        public ValueTask DisposeAsync()
        {
            state.DisposedScopes.Enqueue(_scopeId);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public ConcurrentQueue<(LogLevel Level, Exception? Exception)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Enqueue((logLevel, exception));
    }

    private sealed class QueueFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        public PublisherState State { get; } = new();
        public RecordingLogger<NotificationQueue> QueueLogger { get; } = new();
        public RecordingLogger<NotificationQueueService> ServiceLogger { get; } = new();
        public NotificationQueue Queue { get; }
        public NotificationQueueService Service { get; }

        public QueueFixture(int capacity = 10, int workerCount = 1)
        {
            var options = Options.Create(new NotificationQueueOptions { Capacity = capacity, WorkerCount = workerCount });
            var services = new ServiceCollection();
            services.AddSingleton(State);
            services.AddScoped<IPublisher, RecordingPublisher>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            Queue = new NotificationQueue(options, QueueLogger);
            Service = new NotificationQueueService(Queue, _provider.GetRequiredService<IServiceScopeFactory>(), options, ServiceLogger);
        }

        public Task StopAsync() => WaitAsync(Service.StopAsync(TestContext.Current.CancellationToken));

        public async ValueTask DisposeAsync()
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Service.StopAsync(deadline.Token);
            if (Service.ExecuteTask is not null)
                await WaitAsync(Service.ExecuteTask);
            Service.Dispose();
            await _provider.DisposeAsync();
        }
    }
}
