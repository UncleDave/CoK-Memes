using ChampionsOfKhazad.Bot.OpenAi.Embeddings;
using ChampionsOfKhazad.Bot.Pinecone;
using Pinecone;

namespace ChampionsOfKhazad.Bot.Lore;

internal class LoreService(IStoreLore loreStore, EmbeddingsService embeddingsService, IndexService indexService)
    : IGetLore,
        IGetRelatedLore,
        IUpdateLore,
        ICreateLore
{
    public Task<IReadOnlyList<Lore>> GetLoreAsync(CancellationToken cancellationToken = default) => loreStore.ReadLoreAsync(cancellationToken);

    public Task<IReadOnlyList<MemberLore>> GetMemberLoreAsync(CancellationToken cancellationToken = default) =>
        loreStore.ReadMemberLoreAsync(cancellationToken);

    public Task<Lore> GetLoreAsync(string name, CancellationToken cancellationToken = default) => loreStore.ReadLoreAsync(name, cancellationToken);

    public Task<MemberLore> GetMemberLoreAsync(string name, CancellationToken cancellationToken = default) =>
        loreStore.ReadMemberLoreAsync(name.Split("-").Last(), cancellationToken);

    public async Task UpdateLoreAsync(Lore lore)
    {
        await loreStore.UpsertLoreAsync(lore);
        await UpsertEmbeddingsAsync(lore);
    }

    public async Task UpdateMemberLoreAsync(MemberLore lore)
    {
        await loreStore.UpsertMemberLoreAsync(lore);
        await UpsertEmbeddingsAsync(lore);
    }

    public async Task CreateLoreAsync(params Lore[] lore)
    {
        await loreStore.UpsertLoreAsync(lore);
        await UpsertEmbeddingsAsync(lore);
    }

    public async Task CreateMemberLoreAsync(params MemberLore[] lore)
    {
        await loreStore.UpsertMemberLoreAsync(lore);
        await UpsertEmbeddingsAsync(lore);
    }

    private async Task UpsertEmbeddingsAsync(params IEmbeddable[] embeddables)
    {
        var textEntries = embeddables.Select(x => x.ToTextEntry()).ToArray();
        var embeddings = await embeddingsService.CreateEmbeddingsAsync(textEntries);
        var indexes = await indexService.ListIndexesAsync();

        if (!indexes.Contains(Constants.IndexName))
            await indexService.CreateIndexAsync(Constants.IndexName);

        var index = await indexService.GetIndexAsync(Constants.IndexName);
        var vectors = embeddings.Select(
            x =>
                new Vector
                {
                    Id = x.Id,
                    Values = x.Vector,
                    Metadata = new MetadataMap { { "text", x.Text } }
                }
        );

        await index.Upsert(vectors);
    }

    public async Task<IReadOnlyCollection<string>> GetRelatedLoreAsync(string text, uint max = 10)
    {
        var embeddings = await embeddingsService.CreateEmbeddingsAsync(new TextEntry("input", text));
        var embedding = embeddings.SingleOrDefault();

        var vectorIndex = await indexService.GetIndexAsync(Constants.IndexName);
        var vectors = embedding is not null
            ? await vectorIndex.Query(embedding.Vector, max, includeValues: false, includeMetadata: true)
            : Array.Empty<ScoredVector>();

        return vectors.Select(x => x.Metadata!["text"].Inner!.ToString()!).ToList();
    }
}
