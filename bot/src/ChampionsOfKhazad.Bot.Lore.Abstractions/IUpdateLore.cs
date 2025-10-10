namespace ChampionsOfKhazad.Bot.Lore.Abstractions;

public interface IUpdateLore
{
    Task UpdateLoreAsync(IGuildLore lore);
    Task UpdateLoreAsync(IMemberLore lore);
}
