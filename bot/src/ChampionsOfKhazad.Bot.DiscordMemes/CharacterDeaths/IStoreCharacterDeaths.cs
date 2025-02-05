namespace ChampionsOfKhazad.Bot.DiscordMemes.CharacterDeaths;

public interface IStoreCharacterDeaths
{
    Task InsertCharacterDeathAsync(CharacterDeath characterDeath);
}
