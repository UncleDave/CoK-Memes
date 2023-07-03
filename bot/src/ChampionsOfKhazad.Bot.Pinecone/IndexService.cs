using Pinecone;
using Pinecone.Grpc;

namespace ChampionsOfKhazad.Bot.Pinecone;

public class IndexService
{
    private readonly PineconeClient _pineconeClient;

    public IndexService(PineconeClient pineconeClient)
    {
        _pineconeClient = pineconeClient;
    }

    public Task<IndexName[]> ListIndexesAsync() => _pineconeClient.ListIndexes();

    public Task<Index<GrpcTransport>> GetIndexAsync(string name) =>
        _pineconeClient.GetIndex(name);

    public Task CreateIndexAsync(
        string name,
        uint dimensions = 1536,
        Metric metric = Metric.Cosine
    ) => _pineconeClient.CreateIndex(name, dimensions, metric);
}
