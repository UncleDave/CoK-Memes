using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot.GenAi;

public class LorekeeperPersonalityService(ILorekeeperPersonalityStore store, TimeProvider timeProvider, ILogger<LorekeeperPersonalityService> logger)
{
    public async Task<LorekeeperPersonalitySetting> GetAsync(CancellationToken cancellationToken = default)
    {
        LorekeeperPersonalitySetting? setting;
        try
        {
            setting = await store.GetAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not read the Lorekeeper personality setting; using baseline for this request");
            return new LorekeeperPersonalitySetting(LorekeeperTemperament.Baseline);
        }

        // Resolve expiry without writing: an expired read must not overwrite a concurrent admin change.
        return setting is null || setting.ExpiresAtUtc <= timeProvider.GetUtcNow().UtcDateTime
            ? new LorekeeperPersonalitySetting(LorekeeperTemperament.Baseline)
            : setting;
    }

    public async Task<LorekeeperPersonalitySetting> SetAsync(
        LorekeeperTemperament temperament,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!Enum.IsDefined(temperament))
            throw new ArgumentOutOfRangeException(nameof(temperament));
        if (duration is not null && (duration <= TimeSpan.Zero || duration > TimeSpan.FromDays(30)))
            throw new ArgumentOutOfRangeException(nameof(duration));

        var setting = new LorekeeperPersonalitySetting(
            temperament,
            duration is null ? null : timeProvider.GetUtcNow().UtcDateTime.Add(duration.Value)
        );
        await store.SaveAsync(setting, cancellationToken);
        return setting;
    }
}
