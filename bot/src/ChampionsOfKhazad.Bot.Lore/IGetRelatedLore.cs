namespace ChampionsOfKhazad.Bot.Lore;

public interface IGetRelatedLore
{
    Task<IReadOnlyCollection<string>> GetRelatedLoreAsync(string text, uint max = 10);
}
