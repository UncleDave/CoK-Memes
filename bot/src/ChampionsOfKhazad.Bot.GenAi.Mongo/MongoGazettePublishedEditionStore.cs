using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.GenAi.Mongo;

internal sealed class MongoGazettePublishedEditionStore(IMongoCollection<GazettePublishedEdition> collection) : IGazettePublishedEditionStore
{
    public Task<GazettePublishedEdition?> GetAsync(string id, CancellationToken cancellationToken) =>
        collection.Find(edition => edition.Id == id).FirstOrDefaultAsync(cancellationToken)!;

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
