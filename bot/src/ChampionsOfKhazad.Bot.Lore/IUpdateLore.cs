namespace ChampionsOfKhazad.Bot.Lore;

public interface IUpdateLore
{
    Task UpdateLoreAsync(Lore lore);
    Task UpdateMemberLoreAsync(MemberLore lore);
}
