namespace ChampionsOfKhazad.Bot.Lore;

public interface ICreateLore
{
    Task CreateLoreAsync(params Lore[] lore);
    Task CreateMemberLoreAsync(params MemberLore[] lore);
}
