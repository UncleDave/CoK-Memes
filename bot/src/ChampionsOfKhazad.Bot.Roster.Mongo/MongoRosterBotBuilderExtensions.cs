using ChampionsOfKhazad.Bot.Mongo;
using ChampionsOfKhazad.Bot.Roster;
using ChampionsOfKhazad.Bot.Roster.Mongo;
using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

public static class MongoRosterBotBuilderExtensions
{
    public static BotBuilder AddRosterMongoPersistence(this BotBuilder builder)
    {
        builder
            .AddMongo()
            .AddCollection<RosterState>("guildRosters")
            .AddCollection<RosterNotice>(
                "guildRosterNotices",
                collection =>
                    collection.Indexes.CreateOne(
                        new CreateIndexModel<RosterNotice>(
                            Builders<RosterNotice>.IndexKeys.Ascending(item => item.ScopeId).Ascending(item => item.SentAtUtc)
                        )
                    )
            )
            .Services.AddSingleton<IRosterStore, MongoRosterStore>()
            .AddSingleton<IRosterNoticeStore, MongoRosterNoticeStore>();
        return builder;
    }
}
