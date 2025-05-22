namespace ChampionsOfKhazad.Bot.Lore;

public interface IGetRelatedLore
{
    Task<IReadOnlyList<Lore>> GetRelatedLoreAsync(string text, ushort max = 10);
}
