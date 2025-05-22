namespace ChampionsOfKhazad.Bot.GenAi;

public interface IGetRelatedLore
{
    Task<IReadOnlyList<string>> GetRelatedLoreAsync(string input, ushort max = 10, CancellationToken cancellationToken = default);
}
