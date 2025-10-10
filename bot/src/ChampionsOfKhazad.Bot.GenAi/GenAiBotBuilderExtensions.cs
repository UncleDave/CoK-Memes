using Azure.Storage;
using ChampionsOfKhazad.Bot.Core;
using ChampionsOfKhazad.Bot.GenAi;
using ChampionsOfKhazad.Bot.GenAi.Embeddings;
using Microsoft.Extensions.Azure;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Web.Google;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class GenAiBotBuilderExtensions
{
    public static GenAiBuilder AddGenAi<TEmojiHandler>(this BotBuilder builder, Action<GenAiConfig> configurator)
        where TEmojiHandler : class, IEmojiHandler
    {
        var config = new GenAiConfig();

        configurator(config);

        if (config.OpenAiApiKey is null)
            throw new MissingConfigurationValueException("OpenAiApiKey");

        if (config.GoogleSearchEngineId is null)
            throw new MissingConfigurationValueException("GoogleSearchEngineId");

        if (config.GoogleSearchEngineApiKey is null)
            throw new MissingConfigurationValueException("GoogleSearchEngineApiKey");

        if (config.AzureStorageAccountName is null)
            throw new MissingConfigurationValueException("AzureStorageAccountName");

        if (config.AzureStorageAccountAccessKey is null)
            throw new MissingConfigurationValueException("AzureStorageAccountAccessKey");

        var googleTextSearch = new GoogleTextSearch(config.GoogleSearchEngineId, config.GoogleSearchEngineApiKey);
        var imageGenerationHttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        builder
            .Services.AddKernel()
            .AddOpenAIChatCompletion(Constants.DefaultCompletionsModel, config.OpenAiApiKey)
            .AddOpenAITextToImage(config.OpenAiApiKey, modelId: Constants.DefaultImageModel, httpClient: imageGenerationHttpClient)
            .AddOpenAITextEmbeddingGeneration(Constants.DefaultEmbeddingModel, config.OpenAiApiKey)
            .Plugins.AddFromType<TimePlugin>()
            .AddFromType<ImageGenerationPlugin>()
            .Add(googleTextSearch.CreateWithGetSearchResults("GoogleSearchPlugin"));

        builder.Services.AddAzureClients(azureBuilder =>
        {
            azureBuilder.AddBlobServiceClient(
                new Uri($"https://{config.AzureStorageAccountName}.blob.core.windows.net"),
                new StorageSharedKeyCredential(config.AzureStorageAccountName, config.AzureStorageAccountAccessKey)
            );
        });

        builder
            .Services.AddScoped<ICompletionService, CompletionService>()
            .AddScoped<IEmojiHandler, TEmojiHandler>()
            .AddSingleton(config.ImageGeneration)
            .AddSingleton<ImageStorageService>()
            .AddScoped<LorekeeperPersonality>()
            .AddScoped<SycophantPersonality>()
            .AddScoped<ContrarianPersonality>()
            .AddScoped<DisappointedTeacherPersonality>()
            .AddScoped<CondescendingTeacherPersonality>()
            .AddScoped<NoNutNovemberExpertPersonality>()
            .AddScoped<RatExpertPersonality>()
            .AddScoped<StonerBroPersonality>()
            .AddScoped<HarassmentLawyerPersonality>()
            .AddScoped<ProHarassmentLawyerPersonality>()
            .AddScoped<IEmbeddingsService, EmbeddingsService>();

        return new GenAiBuilder(builder.Services, builder.BotConfiguration);
    }
}
