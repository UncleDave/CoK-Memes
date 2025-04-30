using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.GenAi.Mongo;

internal class MongoGeneratedImageStore(IMongoCollection<GeneratedImage> generatedImageCollection) : IGeneratedImageStore
{
    public async Task<ushort> GetDailyGeneratedImageCountAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var result = await generatedImageCollection.CountDocumentsAsync(
            x => x.UserId == userId && x.Timestamp.Date == DateTime.UtcNow.Date,
            cancellationToken: cancellationToken
        );

        return (ushort)result;
    }

    public Task SaveGeneratedImageAsync(GeneratedImage image) => generatedImageCollection.InsertOneAsync(image);
}
