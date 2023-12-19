namespace ChampionsOfKhazad.Bot.Lore;

public interface IUpdateLore
{
    Task UpdateLoreAsync(GuildLore lore);
    Task UpdateLoreAsync(MemberLore lore);
}
