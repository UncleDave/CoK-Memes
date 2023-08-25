namespace ChampionsOfKhazad.Bot.HardcoreStats.CharacterDeaths;

public interface IStoreCharacterDeaths
{
    Task InsertCharacterDeathAsync(CharacterDeath characterDeath);
}
