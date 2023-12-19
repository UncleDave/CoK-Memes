namespace ChampionsOfKhazad.Bot.Lore;

public interface IGetRelatedLore
{
    Task<IReadOnlyList<Lore>> GetRelatedLoreAsync(string text, uint max = 10);
}
