using ChampionsOfKhazad.Bot.GenAi;
using ChampionsOfKhazad.Bot.GenAi.Mongo;
using MongoDB.Driver;
using static ChampionsOfKhazad.Bot.Tests.DiscordConversationFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class MongoGeneratedImageStoreTests
{
    [Fact]
    public async Task ZeroPageSizeReturnsNoImagesWithoutIssuingAnUnlimitedMongoQuery()
    {
        var collection = Stub<IMongoCollection<GeneratedImage>>((method, _) => throw new InvalidOperationException(method.Name));

        var images = await new MongoGeneratedImageStore(collection).GetAsync(take: 0, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(images);
    }

    [Fact]
    public async Task NonzeroPageSizeKeepsTheRequestedDatabaseLimitAndOffset()
    {
        var queried = false;
        var cursor = Stub<IAsyncCursor<GeneratedImage>>(
            (method, _) =>
                method.Name switch
                {
                    "MoveNextAsync" => Task.FromResult(false),
                    "Dispose" => null,
                    _ => throw new NotSupportedException(method.Name),
                }
        );
        var collection = Stub<IMongoCollection<GeneratedImage>>(
            (method, args) =>
            {
                Assert.Equal("FindAsync", method.Name);
                queried = true;
                var options = Assert.IsAssignableFrom<FindOptions<GeneratedImage, GeneratedImage>>(args![1]);
                Assert.Equal(20, options.Skip);
                Assert.Equal(16, options.Limit);
                Assert.Equal(TestContext.Current.CancellationToken, args[^1]);
                return Task.FromResult(cursor);
            }
        );

        await new MongoGeneratedImageStore(collection).GetAsync(skip: 20, take: 16, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(queried);
    }
}
