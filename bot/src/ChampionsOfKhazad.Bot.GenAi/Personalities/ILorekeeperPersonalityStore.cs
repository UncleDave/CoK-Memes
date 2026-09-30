namespace ChampionsOfKhazad.Bot.GenAi;

public interface ILorekeeperPersonalityStore
{
    Task<LorekeeperPersonalitySetting?> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(LorekeeperPersonalitySetting setting, CancellationToken cancellationToken = default);
}
