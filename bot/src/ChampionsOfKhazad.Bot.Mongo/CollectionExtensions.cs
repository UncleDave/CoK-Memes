using System.Linq.Expressions;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Mongo;

public static class CollectionExtensions
{
    public static void CreateUniqueIndex<T>(this IMongoCollection<T> collection, Expression<Func<T, object>> field, Collation? collation = null) =>
        collection.Indexes.CreateOne(
            new CreateIndexModel<T>(
                Builders<T>.IndexKeys.Ascending(field),
                new CreateIndexOptions
                {
                    Unique = true,
                    Background = true,
                    Collation = collation
                }
            )
        );
}
