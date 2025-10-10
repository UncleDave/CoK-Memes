using ChampionsOfKhazad.Bot.Core;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Embeddings;

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

        builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var client = new OpenAIClient(config.OpenAiApiKey);
            var embeddingClient = client.GetEmbeddingClient(config.Model);
            return new OpenAIEmbeddingGenerator(embeddingClient);
        });

        return builder;
    }
}

public class EmbeddingsConfiguration
{
    public string? OpenAiApiKey { get; set; }
    public string? Model { get; set; }
}

internal class OpenAIEmbeddingGenerator(EmbeddingClient client) : IEmbeddingGenerator<string, Embedding<float>>
{
    public EmbeddingGeneratorMetadata Metadata => new("openai");

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        Microsoft.Extensions.AI.EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var inputs = values.ToList();
        var response = await client.GenerateEmbeddingsAsync(inputs, cancellationToken: cancellationToken);

        var embeddings = response.Value.Select(e => new Embedding<float>(e.ToFloats().ToArray())).ToList();

        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
        // Nothing to dispose
    }
}
