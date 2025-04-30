namespace ChampionsOfKhazad.Bot.GenAi;

public interface IGeneratedImageStore
{
    Task<ushort> GetDailyGeneratedImageCountAsync(ulong userId, CancellationToken cancellationToken = default);
    Task SaveGeneratedImageAsync(GeneratedImage image);
}
