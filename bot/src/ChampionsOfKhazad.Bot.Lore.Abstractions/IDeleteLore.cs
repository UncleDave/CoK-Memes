namespace ChampionsOfKhazad.Bot.Lore.Abstractions;

public interface IDeleteLore
{
    Task DeleteLoreAsync(string name);
}
