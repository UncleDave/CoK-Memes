namespace ChampionsOfKhazad.Bot.Lore.Abstractions;

public interface ICreateLore
{
    Task CreateLoreAsync(IGuildLore lore);
    Task CreateLoreAsync(IMemberLore lore);
}
