using Pinecone;
using Pinecone.Grpc;

namespace ChampionsOfKhazad.Bot.Pinecone;

public class IndexService(PineconeClient pineconeClient)
{
    public Task<IndexName[]> ListIndexesAsync() => pineconeClient.ListIndexes();

    public Task<Index<GrpcTransport>> GetIndexAsync(string name) => pineconeClient.GetIndex(name);

    public Task CreateIndexAsync(string name, uint dimensions = 1536, Metric metric = Metric.Cosine) =>
        pineconeClient.CreateIndex(name, dimensions, metric);
}
