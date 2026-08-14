using ChampionsOfKhazad.Bot.Core;
using ChampionsOfKhazad.Bot.GenAi.Embeddings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class EmbeddingsBotBuilderExtensions
{
    public static BotBuilder AddEmbeddings(this BotBuilder builder, Action<GenAiEmbeddingsConfig> configure)
    {
        var config = new GenAiEmbeddingsConfig();
        configure(config);

        if (config.OpenAiApiKey is null)
            throw new MissingConfigurationValueException("OpenAiApiKey");

        var embeddingClient = new EmbeddingClient(Constants.DefaultEmbeddingModel, config.OpenAiApiKey);

        builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(serviceProvider => new DiagnosticEmbeddingGenerator(
            embeddingClient.AsIEmbeddingGenerator(),
            serviceProvider.GetRequiredService<ILogger<DiagnosticEmbeddingGenerator>>()
        ));

        return builder;
    }
}
