using ChampionsOfKhazad.Bot.Core;
using ChampionsOfKhazad.Bot.GenAi.Embeddings;

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

        builder.Services.AddOpenAIEmbeddingGenerator(Constants.DefaultEmbeddingModel, config.OpenAiApiKey);

        return builder;
    }
}
