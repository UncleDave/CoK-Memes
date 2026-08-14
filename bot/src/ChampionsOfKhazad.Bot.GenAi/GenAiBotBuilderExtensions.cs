using System.ClientModel;
using Azure.Storage;
using ChampionsOfKhazad.Bot.Core;
using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Images;

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

        if (config.AzureStorageAccountName is null)
            throw new MissingConfigurationValueException("AzureStorageAccountName");

        if (config.AzureStorageAccountAccessKey is null)
            throw new MissingConfigurationValueException("AzureStorageAccountAccessKey");

        builder.Services.AddSingleton(new ChatClient(Constants.DefaultCompletionsModel, config.OpenAiApiKey));
        builder.Services.AddSingleton<IChatClient>(serviceProvider =>
        {
            var chatClient = new DiagnosticChatClient(
                serviceProvider.GetRequiredService<ChatClient>().AsIChatClient(),
                serviceProvider.GetRequiredService<ILogger<DiagnosticChatClient>>()
            );

            return new ChatClientBuilder(chatClient).UseFunctionInvocation().Build();
        });
        builder.Services.AddSingleton(
            new ImageClient(
                Constants.DefaultImageModel,
                new ApiKeyCredential(config.OpenAiApiKey),
                new OpenAIClientOptions { NetworkTimeout = TimeSpan.FromMinutes(5) }
            )
        );
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
            .AddSingleton<ImageGenerationService>()
            .AddSingleton<PersonalityTools>()
            .AddScoped<LorekeeperPersonality>()
            .AddScoped<SycophantPersonality>()
            .AddScoped<ContrarianPersonality>()
            .AddScoped<DisappointedTeacherPersonality>()
            .AddScoped<CondescendingTeacherPersonality>()
            .AddScoped<NoNutNovemberExpertPersonality>()
            .AddScoped<RatExpertPersonality>()
            .AddScoped<StonerBroPersonality>();

        return new GenAiBuilder(builder.Services, builder.BotConfiguration);
    }
}
