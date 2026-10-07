using ChampionsOfKhazad.Bot.DiscordMemes.CharacterDeaths;
using ChampionsOfKhazad.Bot.DiscordMemes.StreakBreaks;
using ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;
using ChampionsOfKhazad.Bot.GenAi;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ChampionsOfKhazad.Bot.Tests;

public class FeatureRegistrationTests
{
    [Fact]
    public void FeatureRegistrationsKeepOneBuilderAndTheirExistingLifetimes()
    {
        var services = new ServiceCollection();
        var builder = services.AddBot(_ => { });

        Assert.Same(builder, builder.AddGuildLore());
        Assert.Same(builder, builder.AddDiscordMemes());
        Assert.Same(builder, builder.AddLoreMongoPersistence());
        Assert.Same(builder, builder.AddDiscordMemesMongoPersistence());
        Assert.Same(builder, builder.AddGenAiMongoPersistence());

        AssertSingleton<IGetLore>(services);
        AssertSingleton<ICreateLore>(services);
        AssertSingleton<IUpdateLore>(services);
        AssertSingleton<IDeleteLore>(services);
        AssertSingleton<IStoreLore>(services);
        AssertSingleton<IWordOfTheDayStore>(services);
        AssertSingleton<IGetStreakBreaks>(services);
        AssertSingleton<IStoreStreakBreaks>(services);
        AssertSingleton<IStoreCharacterDeaths>(services);
        AssertSingleton<IGeneratedImageStore>(services);
        AssertSingleton<INotebookStore>(services);
        AssertSingleton<ILorekeeperPersonalityStore>(services);
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(INotificationHandler<CharacterDeathReported>));
    }

    private static void AssertSingleton<T>(IServiceCollection services) =>
        Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services, descriptor => descriptor.ServiceType == typeof(T)).Lifetime);

    [Fact]
    public void GenAiRegistrationKeepsTheBuilderAndScopedRequestServices()
    {
        var services = new ServiceCollection();
        var builder = services.AddBot(_ => { });

        Assert.Same(
            builder,
            builder.AddGenAi<NoopEmojiHandler>(config =>
            {
                config.OpenAiApiKey = "unused-test-key";
                config.AzureStorageAccountName = "testaccount";
                config.AzureStorageAccountAccessKey = Convert.ToBase64String(new byte[32]);
            })
        );

        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICompletionService)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IEmojiHandler)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, descriptor => descriptor.ServiceType == typeof(INotebookDiscoverer)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Scoped,
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(NotebookObserverService)).Lifetime
        );
        AssertSingleton<TimeProvider>(services);
    }

    private sealed class NoopEmojiHandler : IEmojiHandler
    {
        public IEnumerable<string> GetEmojis() => [];

        public string ProcessMessage(string message) => message;
    }
}
