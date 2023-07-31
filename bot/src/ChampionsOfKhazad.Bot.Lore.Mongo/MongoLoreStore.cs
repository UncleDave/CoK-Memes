using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Lore.Mongo;

internal class MongoLoreStore : IStoreLore
{
    private readonly IMongoCollection<Lore> _loreCollection;
    private readonly IMongoCollection<MemberLore> _memberLoreCollection;

    public MongoLoreStore(IMongoCollection<Lore> loreCollection, IMongoCollection<MemberLore> memberLoreCollection)
    {
        _loreCollection = loreCollection;
        _memberLoreCollection = memberLoreCollection;
    }

    public async Task<IReadOnlyList<Lore>> ReadLoreAsync(CancellationToken cancellationToken = default)
    {
        var result = await _loreCollection.FindAsync(FilterDefinition<Lore>.Empty, cancellationToken: cancellationToken);

        return await result.ToListAsync(cancellationToken);
    }

    public async Task<Lore> ReadLoreAsync(string name, CancellationToken cancellationToken = default)
    {
        var result = await _loreCollection.FindAsync(x => x.Name == name, cancellationToken: cancellationToken);

        return await result.SingleOrDefaultAsync(cancellationToken);
    }

    public Task UpsertLoreAsync(params Lore[] lore) => Task.WhenAll(lore.Select(UpsertLoreAsync));

    public Task UpsertLoreAsync(Lore lore) => _loreCollection.ReplaceOneAsync(x => x.Name == lore.Name, lore, new ReplaceOptions { IsUpsert = true });

    public async Task<IReadOnlyList<MemberLore>> ReadMemberLoreAsync(CancellationToken cancellationToken = default)
    {
        var result = await _memberLoreCollection.FindAsync(FilterDefinition<MemberLore>.Empty, cancellationToken: cancellationToken);

        return await result.ToListAsync(cancellationToken);
    }

    public async Task<MemberLore> ReadMemberLoreAsync(string name, CancellationToken cancellationToken = default)
    {
        var result = await _memberLoreCollection.FindAsync(x => x.Name == name, cancellationToken: cancellationToken);

        return await result.SingleOrDefaultAsync(cancellationToken);
    }

    public Task UpsertMemberLoreAsync(params MemberLore[] lore) => Task.WhenAll(lore.Select(UpsertMemberLoreAsync));

    public Task UpsertMemberLoreAsync(MemberLore lore) =>
        _memberLoreCollection.ReplaceOneAsync(x => x.Name == lore.Name, lore, new ReplaceOptions { IsUpsert = true });
}
