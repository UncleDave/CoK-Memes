namespace ChampionsOfKhazad.Bot.HardcoreStats.CharacterDeaths;

public interface IRecordCharacterDeaths
{
    Task RecordCharacterDeathAsync(CharacterDeath characterDeath);
}
