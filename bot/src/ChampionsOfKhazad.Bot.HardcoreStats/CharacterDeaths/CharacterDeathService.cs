namespace ChampionsOfKhazad.Bot.HardcoreStats.CharacterDeaths;

internal class CharacterDeathService : IRecordCharacterDeaths
{
    private readonly IStoreCharacterDeaths _deathStore;

    public CharacterDeathService(IStoreCharacterDeaths deathStore)
    {
        _deathStore = deathStore;
    }

    public Task RecordCharacterDeathAsync(CharacterDeath characterDeath) => _deathStore.InsertCharacterDeathAsync(characterDeath);
}
