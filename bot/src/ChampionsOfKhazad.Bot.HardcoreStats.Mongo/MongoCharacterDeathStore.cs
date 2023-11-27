using ChampionsOfKhazad.Bot.HardcoreStats.CharacterDeaths;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.HardcoreStats.Mongo;

internal class MongoCharacterDeathStore(IMongoCollection<CharacterDeath> characterDeathCollection) : IStoreCharacterDeaths
{
    public Task InsertCharacterDeathAsync(CharacterDeath characterDeath) => characterDeathCollection.InsertOneAsync(characterDeath);
}
