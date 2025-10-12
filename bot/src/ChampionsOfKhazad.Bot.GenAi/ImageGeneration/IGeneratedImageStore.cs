namespace ChampionsOfKhazad.Bot.GenAi;

public interface IGeneratedImageStore
{
    Task<IReadOnlyCollection<GeneratedImage>> GetAsync(
        ushort skip = 0,
        ushort take = 20,
        ulong? userId = null,
        bool sortAscending = false,
        CancellationToken cancellationToken = default
    );
    Task<ushort> GetDailyGeneratedImageCountAsync(ulong userId, CancellationToken cancellationToken = default);
    Task SaveGeneratedImageAsync(GeneratedImage image);
    Task<IReadOnlyCollection<GeneratedImage>> SearchAsync(
        string searchText,
        ushort take = 4,
        ulong? userId = null,
        CancellationToken cancellationToken = default
    );
}
