namespace ChampionsOfKhazad.Bot.GenAi;

public interface IGetRelatedLore
{
    Task<IReadOnlyList<Lore>> GetRelatedLoreAsync(string text, uint max = 10);
}
