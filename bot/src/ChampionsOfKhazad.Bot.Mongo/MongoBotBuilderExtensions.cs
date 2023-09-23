using ChampionsOfKhazad.Bot.Core;
using ChampionsOfKhazad.Bot.Mongo;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson.Serialization.Conventions;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class MongoBotBuilderExtensions
{
    public static MongoBuilder AddMongo(this BotBuilder botBuilder)
    {
        botBuilder.Services.TryAddSingleton(_ =>
        {
            var conventionPack = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true),
                new IgnoreIfNullConvention(true),
            };

            ConventionRegistry.Register("ChampionsOfKhazad.Bot", conventionPack, _ => true);

            return new MongoCollectionProvider(
                botBuilder.BotConfiguration.Persistence.ConnectionString
                    ?? throw new MissingConfigurationValueException(nameof(BotConfiguration.Persistence.ConnectionString))
            );
        });

        return new MongoBuilder(botBuilder.Services, botBuilder.BotConfiguration);
    }
}
