using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.GenAi.Mongo;

internal class MongoGeneratedImageStore(IMongoCollection<GeneratedImage> generatedImageCollection) : IGeneratedImageStore
{
    public async Task<IReadOnlyCollection<GeneratedImage>> GetAsync(
        ushort skip = 0,
        ushort take = 20,
        ulong? userId = null,
        bool sortAscending = false,
        CancellationToken cancellationToken = default
    )
    {
        var filter = userId is not null ? Builders<GeneratedImage>.Filter.Eq(x => x.UserId, userId.Value) : FilterDefinition<GeneratedImage>.Empty;
        var sort = sortAscending
            ? Builders<GeneratedImage>.Sort.Ascending(x => x.Timestamp)
            : Builders<GeneratedImage>.Sort.Descending(x => x.Timestamp);

        return await generatedImageCollection.Find(filter).Skip(skip).Limit(take).Sort(sort).ToListAsync(cancellationToken);
    }

    public async Task<ushort> GetDailyGeneratedImageCountAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        // Done in memory rather than in the database to avoid an issue with DateTimeOffset serialisation.
        var userGeneratedImages = await generatedImageCollection.Find(x => x.UserId == userId).ToListAsync(cancellationToken);
        var imagesGeneratedToday = userGeneratedImages.Count(x => x.Timestamp.Date == DateTime.Now.Date);

        return (ushort)imagesGeneratedToday;
    }

    public Task SaveGeneratedImageAsync(GeneratedImage image) => generatedImageCollection.InsertOneAsync(image);

    public async Task<IReadOnlyCollection<GeneratedImage>> SearchAsync(
        string searchText,
        ushort take = 4,
        ulong? userId = null,
        CancellationToken cancellationToken = default
    )
    {
        var textFilter = Builders<GeneratedImage>.Filter.Text(searchText);
        var filter = userId is not null
            ? Builders<GeneratedImage>.Filter.And(textFilter, Builders<GeneratedImage>.Filter.Eq(x => x.UserId, userId.Value))
            : textFilter;

        return await generatedImageCollection.Find(filter).Limit(take).ToListAsync(cancellationToken);
    }
}
