using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;

namespace ChampionsOfKhazad.Bot.OpenAi.Embeddings;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEmbeddingsService(
        this IServiceCollection services,
        string apiKey
    )
    {
        services.AddHttpClient<EmbeddingsService>(client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/v1/embeddings");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                apiKey
            );
        });

        return services;
    }
}
