using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.EventLoop;

public class NotebookObservationEvent(NotebookObserverService observer, BotContextProvider contextProvider, IOptions<NotebookObserverOptions> options)
    : IEventLoopEvent
{
    public string Name => "NotebookObservation";
    public TimeSpan MeanTimeToHappen => TimeSpan.FromMinutes(options.Value.MeanTimeToHappenMinutes);

    public async Task<bool> EligibleToFire(CancellationToken cancellationToken) =>
        contextProvider.IsReady && await observer.IsEligibleAsync(cancellationToken);

    public Task FireAsync(CancellationToken cancellationToken) =>
        contextProvider.IsReady ? observer.ObserveAsync(cancellationToken) : Task.CompletedTask;
}
