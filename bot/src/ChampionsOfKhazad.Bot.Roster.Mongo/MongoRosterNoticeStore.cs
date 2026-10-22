using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Roster.Mongo;

internal class MongoRosterNoticeStore(IMongoCollection<RosterNotice> collection) : IRosterNoticeStore
{
    public async Task AddPendingAsync(RosterNotice notice, CancellationToken cancellationToken)
    {
        try
        {
            await collection.InsertOneAsync(notice, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey) { }
    }

    public async Task<IReadOnlyList<RosterNotice>> GetPendingAsync(string scopeId, CancellationToken cancellationToken) =>
        await collection
            .Find(notice => notice.ScopeId == scopeId && notice.SentAtUtc == null)
            .SortBy(notice => notice.CreatedAtUtc)
            .ThenBy(notice => notice.Id)
            .ToListAsync(cancellationToken);

    public Task MarkSentAsync(string id, DateTime sentAtUtc, ulong messageId, CancellationToken cancellationToken) =>
        collection.UpdateOneAsync(
            notice => notice.Id == id && notice.SentAtUtc == null,
            Builders<RosterNotice>.Update.Set(notice => notice.SentAtUtc, sentAtUtc).Set(notice => notice.DiscordMessageId, messageId),
            cancellationToken: cancellationToken
        );

    public Task RemoveDepartedPendingAsync(string scopeId, string[] activeCharacterKeys, CancellationToken cancellationToken) =>
        collection.DeleteManyAsync(
            Builders<RosterNotice>.Filter.Eq(notice => notice.ScopeId, scopeId)
                & Builders<RosterNotice>.Filter.Eq(notice => notice.SentAtUtc, null)
                & Builders<RosterNotice>.Filter.Nin(notice => notice.CharacterKey, activeCharacterKeys),
            cancellationToken
        );
}
