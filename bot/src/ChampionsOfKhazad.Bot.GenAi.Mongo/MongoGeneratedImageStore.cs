using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.GenAi.Mongo;

internal class MongoGeneratedImageStore(IMongoCollection<GeneratedImage> generatedImageCollection) : IGeneratedImageStore
{
    public async Task<ushort> GetDailyGeneratedImageCountAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        // Done in memory rather than in the database to avoid an issue with DateTimeOffset serialisation.
        var userGeneratedImages = await generatedImageCollection.Find(x => x.UserId == userId).ToListAsync(cancellationToken);
        var imagesGeneratedToday = userGeneratedImages.Count(x => x.Timestamp.Date == DateTime.UtcNow.Date);

        return (ushort)imagesGeneratedToday;
    }

    public Task SaveGeneratedImageAsync(GeneratedImage image) => generatedImageCollection.InsertOneAsync(image);
}
