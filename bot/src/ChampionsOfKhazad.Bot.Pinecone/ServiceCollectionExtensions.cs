using Microsoft.Extensions.DependencyInjection;
using Pinecone;

namespace ChampionsOfKhazad.Bot.Pinecone;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPinecone(this IServiceCollection services, string apiKey)
    {
        services
            .AddSingleton(new PineconeClient(apiKey, "us-west1-gcp-free"))
            .AddSingleton<IndexService>();

        return services;
    }
}
