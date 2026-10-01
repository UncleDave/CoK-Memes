using System.Net;
using System.Net.Http.Json;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot.Portal.Tests;

public class LoreMutationEndpointTests
{
    [Theory]
    [InlineData("guild-lore")]
    [InlineData("member-lore")]
    public async Task DuplicateCreateReturnsConflictAndPreservesTheOriginalEvenWhenOpenAiIsUnavailable(string route)
    {
        var fixture = new MongoLoreStoreFixture();
        await using var app = await CreateAppAsync(fixture);
        using var client = app.GetTestClient();
        using var first = await client.PostAsync($"/api/{route}", CreateBody(route, "Entry", "Original"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal("/api/lore/Entry", first.Headers.Location!.OriginalString);
        var original = Assert.Single(fixture.Documents);
        fixture.EmbeddingFailure = new InvalidOperationException("OpenAI unavailable");

        using var duplicate = await client.PostAsync(
            $"/api/{route}",
            CreateBody(route, "eNtRy", "Replacement"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var error = await duplicate.Content.ReadFromJsonAsync<Dictionary<string, string>>(TestContext.Current.CancellationToken);
        Assert.Equal("Lore with this name already exists.", error!["message"]);
        Assert.Same(original, Assert.Single(fixture.Documents));
        Assert.Equal(1, fixture.EmbeddingCalls);
    }

    [Theory]
    [InlineData("guild-lore")]
    [InlineData("member-lore")]
    public async Task MissingUpdateReturnsNotFoundWithoutCallingOpenAi(string route)
    {
        var fixture = new MongoLoreStoreFixture { EmbeddingFailure = new InvalidOperationException("OpenAI unavailable") };
        await using var app = await CreateAppAsync(fixture);
        using var client = app.GetTestClient();
        using var response = await client.PutAsync(
            $"/api/{route}/Missing",
            CreateBody(route, "Missing", "Content"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, fixture.EmbeddingCalls);
        Assert.Empty(fixture.Documents);
    }

    [Theory]
    [InlineData("guild-lore")]
    [InlineData("member-lore")]
    public async Task ExistingUpdateStillSucceedsAndKeepsOneEntry(string route)
    {
        var fixture = new MongoLoreStoreFixture();
        await using var app = await CreateAppAsync(fixture);
        using var client = app.GetTestClient();
        using var first = await client.PostAsync($"/api/{route}", CreateBody(route, "Entry", "Original"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var response = await client.PutAsync(
            $"/api/{route}/Entry",
            CreateBody(route, "Entry", "Updated"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("Updated", Assert.Single(fixture.Documents).Content);
        Assert.Equal(2, fixture.EmbeddingCalls);
    }

    [Theory]
    [InlineData("guild-lore")]
    [InlineData("member-lore")]
    public async Task ConcurrentCreatesReturnOneCreatedAndOneConflictThroughTheDuplicateKeyPath(string route)
    {
        var bothInserting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals = 0;
        var fixture = new MongoLoreStoreFixture
        {
            BeforeInsert = async () =>
            {
                if (Interlocked.Increment(ref arrivals) == 2)
                    bothInserting.SetResult();
                await release.Task.WaitAsync(TestContext.Current.CancellationToken);
            },
        };
        await using var app = await CreateAppAsync(fixture);
        using var client = app.GetTestClient();
        var first = client.PostAsync($"/api/{route}", CreateBody(route, "Entry", "First"), TestContext.Current.CancellationToken);
        var second = client.PostAsync($"/api/{route}", CreateBody(route, "eNtRy", "Second"), TestContext.Current.CancellationToken);
        HttpResponseMessage[] responses;
        try
        {
            await bothInserting.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        finally
        {
            release.TrySetResult();
            responses = await Task.WhenAll(first, second);
        }
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], responses.Select(response => response.StatusCode).Order());
        Assert.Single(fixture.Documents);
        Assert.Equal(1, fixture.DuplicateKeyFailures);
    }

    private static async Task<WebApplication> CreateAppAsync(MongoLoreStoreFixture fixture)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.AddBot(_ => { }).AddGuildLore();
        builder.Services.AddSingleton<IStoreLore>(fixture.Store);
        builder.Services.AddSingleton(fixture.Embeddings);
        var app = builder.Build();
        app.UseAuthorization();
        // Isolate HTTP binding/status behavior here; membership authorization has its own tests.
        var policy = new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build();
        app.MapGroup("api").MapLoreMutations(policy);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static JsonContent CreateBody(string route, string name, string content) =>
        route == "guild-lore"
            ? JsonContent.Create(new CreateGuildLoreContract(name, content))
            : JsonContent.Create(new CreateMemberLoreContract(name, "they", "UK", "Character", content, [], []));
}
