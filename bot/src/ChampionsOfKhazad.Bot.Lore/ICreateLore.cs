using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot.Lore;

public interface ICreateLore
{
    Task CreateLoreAsync(GuildLore lore);
    Task CreateLoreAsync(MemberLore lore);
}
