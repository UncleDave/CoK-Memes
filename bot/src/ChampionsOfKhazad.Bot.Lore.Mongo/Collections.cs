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

    public static readonly CreateIndexModel<LoreDocument> MemberDiscordUserIdIndex = new(
        Builders<LoreDocument>.IndexKeys.Ascending(x => x.DiscordUserId),
        new CreateIndexOptions<LoreDocument>
        {
            Name = "unique_active_member_discord_user_id",
            Unique = true,
            PartialFilterExpression = Builders<LoreDocument>.Filter.Gt(x => x.DiscordUserId, 0UL),
        }
    );
}
