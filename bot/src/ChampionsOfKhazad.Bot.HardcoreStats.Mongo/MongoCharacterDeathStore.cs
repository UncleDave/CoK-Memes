using ChampionsOfKhazad.Bot.HardcoreStats.CharacterDeaths;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.HardcoreStats.Mongo;

internal class MongoCharacterDeathStore : IStoreCharacterDeaths
{
    private readonly IMongoCollection<CharacterDeath> _characterDeathCollection;

    public MongoCharacterDeathStore(IMongoCollection<CharacterDeath> characterDeathCollection)
    {
        _characterDeathCollection = characterDeathCollection;
    }

    public Task InsertCharacterDeathAsync(CharacterDeath characterDeath) => _characterDeathCollection.InsertOneAsync(characterDeath);
}
