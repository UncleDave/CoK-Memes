using ChampionsOfKhazad.Bot.Core;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class EmbeddingsBotBuilderExtensions
{
    public static BotBuilder AddEmbeddings(this BotBuilder builder, Action<EmbeddingsConfiguration> configure)
    {
        var config = new EmbeddingsConfiguration();
        configure(config);

        if (config.OpenAiApiKey is null)
            throw new MissingConfigurationValueException("OpenAiApiKey");

        if (config.Model is null)
            throw new MissingConfigurationValueException("Model");

        builder.Services.AddKernel().AddOpenAITextEmbeddingGeneration(config.Model, config.OpenAiApiKey);

        // Register the ITextEmbeddingGenerationService from Kernel
        builder.Services.AddSingleton<ITextEmbeddingGenerationService>(sp =>
            sp.GetRequiredService<Kernel>().GetRequiredService<ITextEmbeddingGenerationService>()
        );

        return builder;
    }
}

public class EmbeddingsConfiguration
{
    public string? OpenAiApiKey { get; set; }
    public string? Model { get; set; }
}
