using ChampionsOfKhazad.Bot.DiscordMemes.CharacterDeaths;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.DiscordMemes.Mongo;

internal class MongoCharacterDeathStore(IMongoCollection<CharacterDeath> characterDeathCollection) : IStoreCharacterDeaths
{
    public Task InsertCharacterDeathAsync(CharacterDeath characterDeath) => characterDeathCollection.InsertOneAsync(characterDeath);
}
