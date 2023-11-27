namespace ChampionsOfKhazad.Bot.HardcoreStats.CharacterDeaths;

internal class CharacterDeathService(IStoreCharacterDeaths deathStore) : IRecordCharacterDeaths
{
    public Task RecordCharacterDeathAsync(CharacterDeath characterDeath) => deathStore.InsertCharacterDeathAsync(characterDeath);
}
