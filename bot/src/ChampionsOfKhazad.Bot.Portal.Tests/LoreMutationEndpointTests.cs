using System.Net;
using System.Net.Http.Json;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot.Portal.Tests;

public class LoreMutationEndpointTests
{
    [Theory]
    [InlineData("guild-lore")]
    [InlineData("member-lore")]
    public async Task SuccessfulCreateReturnsCreatedAndBindsTheExpectedLore(string route)
    {
        var store = new LoreStoreStub();
        await using var app = await CreateAppAsync(store);
        using var client = app.GetTestClient();
        using var response = await client.PostAsync($"/api/{route}", CreateBody(route, "Entry", "Original"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/lore/Entry", response.Headers.Location!.OriginalString);
        Assert.NotNull(store.Created);
        Assert.Equal("Entry", store.Created.Name);
        Assert.Equal("Original", route == "guild-lore" ? ((IGuildLore)store.Created).Content : ((IMemberLore)store.Created).Biography);
        Assert.Null(store.Updated);
    }

    [Theory]
    [InlineData("guild-lore")]
    [InlineData("member-lore")]
    public async Task RejectedCreateReturnsConflictWithoutTryingAnUpdate(string route)
    {
        var store = new LoreStoreStub { CreateResult = false };
        await using var app = await CreateAppAsync(store);
        using var client = app.GetTestClient();
        using var response = await client.PostAsync(
            $"/api/{route}",
            CreateBody(route, "Entry", "Replacement"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(TestContext.Current.CancellationToken);
        Assert.Equal("Lore with this name already exists.", error!["message"]);
        Assert.NotNull(store.Created);
        Assert.Null(store.Updated);
    }

    [Theory]
    [InlineData("guild-lore")]
    [InlineData("member-lore")]
    public async Task MissingUpdateReturnsNotFoundWithoutTryingACreate(string route)
    {
        var store = new LoreStoreStub { UpdateResult = false };
        await using var app = await CreateAppAsync(store);
        using var client = app.GetTestClient();
        using var response = await client.PutAsync(
            $"/api/{route}/Missing",
            CreateBody(route, "Missing", "Content"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(store.Updated);
        Assert.Null(store.Created);
    }

    [Theory]
    [InlineData("guild-lore")]
    [InlineData("member-lore")]
    public async Task SuccessfulUpdateReturnsNoContentAndBindsTheExpectedLore(string route)
    {
        var store = new LoreStoreStub();
        await using var app = await CreateAppAsync(store);
        using var client = app.GetTestClient();

        using var response = await client.PutAsync(
            $"/api/{route}/Entry",
            CreateBody(route, "Entry", "Updated"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull(store.Updated);
        Assert.Equal("Entry", store.Updated.Name);
        Assert.Equal("Updated", route == "guild-lore" ? ((IGuildLore)store.Updated).Content : ((IMemberLore)store.Updated).Biography);
        Assert.Null(store.Created);
    }

    [Theory]
    [InlineData("guild-lore")]
    [InlineData("member-lore")]
    public async Task StoreFailuresPropagateInsteadOfReturningSuccessfulResults(string route)
    {
        var failure = new InvalidOperationException("Store unavailable");
        var store = new LoreStoreStub { WriteFailure = failure };
        await using var app = await CreateAppAsync(store);
        using var client = app.GetTestClient();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.PostAsync($"/api/{route}", CreateBody(route, "Entry", "Content"), TestContext.Current.CancellationToken)
        );
        Assert.Same(failure, actual);
    }

    private static async Task<WebApplication> CreateAppAsync(IStoreLore store)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.AddBot(_ => { }).AddGuildLore();
        builder.Services.RemoveAll<IGetRelatedLore>();
        builder.Services.AddSingleton(store);
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
