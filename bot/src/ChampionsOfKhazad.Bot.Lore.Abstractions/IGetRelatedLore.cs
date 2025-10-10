namespace ChampionsOfKhazad.Bot.Lore.Abstractions;

public interface IGetRelatedLore
{
    Task<IReadOnlyList<Lore>> GetRelatedLoreAsync(string text, uint max = 10);
}
