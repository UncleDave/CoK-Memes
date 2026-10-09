using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.GenAi.Mongo;

internal sealed class MongoGazettePublishedEditionStore(IMongoCollection<GazettePublishedEdition> collection) : IGazettePublishedEditionStore
{
    public Task<GazettePublishedEdition?> GetAsync(string id, CancellationToken cancellationToken) =>
        collection.Find(edition => edition.Id == id).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<IReadOnlyList<GazettePublishedEdition>> GetRecentAsync(
        ulong guildId,
        ulong channelId,
        DateTime sinceUtc,
        DateTime untilUtc,
        int maximumEditions,
        CancellationToken cancellationToken
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumEditions, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumEditions, 5);
        var filters = Builders<GazettePublishedEdition>.Filter;
        var filter =
            filters.Eq(edition => edition.GuildId, guildId)
            & filters.Eq(edition => edition.ChannelId, channelId)
            & filters.Gte(edition => edition.ApprovedAtUtc, sinceUtc)
            & filters.Lte(edition => edition.ApprovedAtUtc, untilUtc);
        var sort = Builders<GazettePublishedEdition>.Sort.Descending(edition => edition.ApprovedAtUtc).Descending(edition => edition.Id);
        return await collection.Find(filter).Sort(sort).Limit(maximumEditions).ToListAsync(cancellationToken);
    }

    public Task SaveAsync(GazettePublishedEdition edition, CancellationToken cancellationToken) =>
        collection.InsertOneAsync(edition, cancellationToken: cancellationToken);

    public async Task ConfirmMessageAsync(string id, ulong messageId, CancellationToken cancellationToken)
    {
        var result = await collection.UpdateOneAsync(
            edition => edition.Id == id && (edition.MessageId == null || edition.MessageId == messageId),
            Builders<GazettePublishedEdition>.Update.Set(edition => edition.MessageId, messageId),
            cancellationToken: cancellationToken
        );
        if (result.MatchedCount != 1)
            throw new InvalidOperationException("Gazette published-message identity changed.");
    }
}
