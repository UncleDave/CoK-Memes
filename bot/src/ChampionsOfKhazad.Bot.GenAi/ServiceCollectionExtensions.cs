using ChampionsOfKhazad.Bot.Core;
using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.MongoDB;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Core;
using MongoDB.Driver;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGenAi<TEmojiHandler>(this IServiceCollection services, Action<GenAiConfig> configurator)
        where TEmojiHandler : class, IEmojiHandler
    {
        var config = new GenAiConfig();

        configurator(config);

        if (config.OpenAiApiKey is null)
            throw new MissingConfigurationValueException("OpenAiApiKey");

        if (config.MongoConnectionString is null)
            throw new MissingConfigurationValueException("MongoConnectionString");

        services
            .AddKernel()
            .AddOpenAIChatCompletion(Constants.DefaultCompletionsModel, config.OpenAiApiKey)
            .Plugins.AddFromType<MathPlugin>()
            .AddFromType<TimePlugin>()
            .AddMongoLorekeeperMemoryPlugin(config.MongoConnectionString, config.OpenAiApiKey);

        services
            .AddSingleton<ICompletionService, CompletionService>()
            .AddSingleton<IEmojiHandler, TEmojiHandler>()
            .AddSingleton<LorekeeperPersonality>()
            .AddSingleton<SycophantPersonality>();

        return services;
    }

    private static IKernelBuilderPlugins AddMongoLorekeeperMemoryPlugin(
        this IKernelBuilderPlugins plugins,
        string mongoConnectionString,
        string openAiApiKey
    )
    {
        plugins.Services.AddSingleton<ISemanticTextMemory>(_ =>
        {
            var memoryStore = new MongoDBMemoryStore(mongoConnectionString, new MongoUrl(mongoConnectionString).DatabaseName);
            var embeddingGenerator = new OpenAITextEmbeddingGenerationService(Constants.DefaultEmbeddingModel, openAiApiKey);

            return new SemanticTextMemory(memoryStore, embeddingGenerator);
        });

        plugins.Services.AddSingleton<LorekeeperMemory>();

        return plugins.AddFromType<LorekeeperMemoryPlugin>();
    }
}
