namespace ChampionsOfKhazad.Bot.Lore.Abstractions;

public interface IGetRelatedLore
{
    Task<IReadOnlyList<ILore>> GetRelatedLoreAsync(string text, uint max = 10);
}
