using System.Linq.Expressions;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Lore.Mongo;

internal record Index<T>(Expression<Func<T, object>> Field, Collation Collation);

internal record Collection<T>(string Name, Index<T> UniqueIndex);

internal static class Collections
{
    public static readonly Collection<LoreDocument> Lore = new(
        "lore",
        new Index<LoreDocument>(x => x.Name, new Collation("en", strength: CollationStrength.Primary))
    );
}
