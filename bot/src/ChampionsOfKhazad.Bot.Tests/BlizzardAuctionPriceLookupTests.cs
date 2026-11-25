using System.Net;
using System.Net.Http.Headers;
using ChampionsOfKhazad.Bot.Roster;
using ChampionsOfKhazad.Bot.Roster.Blizzard;

namespace ChampionsOfKhazad.Bot.Tests;

public class BlizzardAuctionPriceLookupTests
{
    private const string Listings = """
        {"id":114,"name":"Horde Auction House","auctions":[
          {"id":1,"item":{"id":2589},"quantity":10,"buyout":3000},
          {"id":2,"item":{"id":2589},"quantity":2,"buyout":700},
          {"id":3,"item":{"id":2589},"quantity":1,"bid":1},
          {"id":4,"item":{"id":77},"quantity":1,"bid":10000},
          {"id":5,"item":{"id":1234},"quantity":3,"buyout":10}
        ]}
        """;
    private const string Commodities = """
        {"auctions":[
          {"id":6,"item":{"id":10620},"quantity":20,"unit_price":2050},
          {"id":7,"item":{"id":10620},"quantity":4,"unit_price":2042}
        ]}
        """;
    private const string Search = """
        {"page":1,"pageCount":6,"results":[
          {"key":{"href":"https://example.test/not-followed"},"data":{"id":10620,"name":{"en_US":"Thorium Ore"}}},
          {"data":{"id":248815,"name":{"en_US":"Heavy Thorium Ore"}}}
        ]}
        """;
    private const string EmptySearch = """{"page":1,"pageCount":0}""";
    private const string BlackskullSearch = """
        {"page":1,"pageCount":1,"results":[
          {"data":{"id":1169,"name":{"en_US":"Blackskull Shield"}}}
        ]}
        """;

    [Fact]
    public async Task ExactNameUsesCommodityUnitPriceAndReturnsAnEstimateAtTheOriginalSnapshotTime()
    {
        using var fixture = new Fixture();
        var quote = await fixture.QuoteAsync(" thorium ore ", 20);

        Assert.Equal("ok", quote.Status);
        Assert.Equal("EU Normal Horde", quote.Market);
        Assert.Equal(new AuctionItem(10620, "Thorium Ore"), quote.Item);
        Assert.Equal(2042m, quote.LowestUnitCopper);
        Assert.Equal("20s 42c", quote.LowestUnitPrice);
        Assert.Equal(20, quote.Quantity);
        Assert.Equal("4g 8s 40c", quote.EstimatedTotal);
        Assert.Equal(fixture.CommoditiesAt, quote.SnapshotAtUtc);
        Assert.False(quote.IsStale);
        Assert.Equal(4, fixture.Requests.Count);
        Assert.Single(fixture.Requests, request => request.Uri.Query.Contains("name.en_US=thorium%20ore", StringComparison.Ordinal));
        Assert.All(fixture.Requests.Where(request => request.Uri.AbsolutePath != "/token"), request =>
        {
            Assert.Equal("eu.api.blizzard.com", request.Uri.Host);
            Assert.Equal("Bearer", request.Scheme);
            Assert.DoesNotContain("test-token", request.Uri.Query);
        });
        Assert.Single(fixture.Requests, request =>
            request.Uri.AbsolutePath == "/data/wow/ruleset/2/auctions/114"
            && request.Uri.Query.Contains("dynamic-forever-eu", StringComparison.Ordinal)
        );
        Assert.Single(fixture.Requests, request => request.Uri.AbsolutePath == "/data/wow/ruleset/2/auctions/114/commodities");
    }

    [Fact]
    public async Task LotBuyoutsAreComparedPerUnitAndBidOnlyItemsHaveNoBuyouts()
    {
        using var fixture = new Fixture();
        var linen = await fixture.QuoteAsync("2589", 20);
        Assert.Equal("ok", linen.Status);
        Assert.Equal(300m, linen.LowestUnitCopper);
        Assert.Equal("60s", linen.EstimatedTotal);
        Assert.Equal(fixture.ListingsAt, linen.SnapshotAtUtc);
        var fractional = await fixture.QuoteAsync("1234", 3);
        Assert.Equal("3.33c", fractional.LowestUnitPrice);
        Assert.Equal("10c", fractional.EstimatedTotal);
        var bidOnly = await fixture.QuoteAsync("77");
        Assert.Equal("no_buyouts", bidOnly.Status);
        Assert.Null(bidOnly.LowestUnitPrice);
        Assert.Equal(3, fixture.Requests.Count);
    }

    [Fact]
    public async Task ReusesLargeSnapshotsAndRevalidatesWithoutMakingTheirPublicationTimeNewer()
    {
        using var fixture = new Fixture { ListingsJson = new string(' ', 2 * 1024 * 1024 + 1) + Listings };
        var first = await fixture.QuoteAsync("10620");
        Assert.Equal("ok", first.Status);
        Assert.Null(first.EstimatedTotal);
        Assert.Null(first.Quantity);
        var fetched = fixture.Requests.Count;
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(59);
        Assert.Equal(first, await fixture.QuoteAsync("10620"));
        Assert.Equal(fetched, fixture.Requests.Count);

        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(2);
        fixture.Revalidate = true;
        Assert.Equal(first, await fixture.QuoteAsync("10620"));
        Assert.Equal(fetched + 2, fixture.Requests.Count);
        Assert.Equal(fixture.ListingsAt, fixture.Requests[^2].ModifiedSince);
        Assert.Equal(fixture.CommoditiesAt, fixture.Requests[^1].ModifiedSince);
    }

    [Fact]
    public async Task AmbiguousOrMissingCatalogueNamesDoNotDownloadTheMarket()
    {
        using var fixture = new Fixture();
        var choices = await fixture.QuoteAsync("Thorium");
        Assert.Equal("ambiguous_item", choices.Status);
        Assert.Equal([10620, 248815], choices.Choices!.Select(item => item.Id).ToArray());
        fixture.SearchJson = EmptySearch;
        Assert.Equal("item_not_found", (await fixture.QuoteAsync("Not an item")).Status);
        Assert.DoesNotContain(fixture.Requests, request => request.Uri.AbsolutePath.Contains("/auctions/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("buyout", "ok", "29g 90s")]
    [InlineData("bid", "no_buyouts", null)]
    public async Task BlackskullShieldCanBeQuotedWhenOnlyRetailReturnsTheName(string priceField, string status, string? price)
    {
        using var fixture = new Fixture
        {
            SearchJson = EmptySearch,
            RetailSearchJson = BlackskullSearch,
            ListingsJson = $$"""{"id":114,"auctions":[{"item":{"id":1169},"quantity":1,"{{priceField}}":299000}]}""",
        };

        var quote = await fixture.QuoteAsync("Blackskull shield");

        Assert.Equal(status, quote.Status);
        Assert.Equal(new AuctionItem(1169, "Blackskull Shield"), quote.Item);
        Assert.Equal(price, quote.LowestUnitPrice);
        Assert.Equal(price is not null ? fixture.ListingsAt : fixture.CommoditiesAt, quote.SnapshotAtUtc);
        Assert.Collection(
            fixture.Requests.Where(request => request.Uri.AbsolutePath == "/data/wow/search/item"),
            request => Assert.Contains("namespace=static-forever-eu&", request.Uri.Query),
            request => Assert.Contains("namespace=static-eu&", request.Uri.Query)
        );
    }

    [Fact]
    public async Task ANameFoundOnlyInRetailDoesNotProduceAQuoteWithoutAMatchingForeverListing()
    {
        using var fixture = new Fixture { SearchJson = EmptySearch, RetailSearchJson = BlackskullSearch };

        var quote = await fixture.QuoteAsync("Blackskull Shield");

        Assert.Equal("item_not_found", quote.Status);
        Assert.Null(quote.Item);
        Assert.Null(quote.SnapshotAtUtc);
        Assert.Contains(fixture.Requests, request => request.Uri.AbsolutePath == "/data/wow/ruleset/2/auctions/114/commodities");
    }

    [Theory]
    [InlineData(false, "ok")]
    [InlineData(true, "ambiguous_item")]
    public async Task OnlyExactRetailMatchesListedInTheForeverMarketAreEligible(bool bothListed, string status)
    {
        using var fixture = new Fixture
        {
            SearchJson = EmptySearch,
            RetailSearchJson = """
                {"page":1,"pageCount":1,"results":[
                  {"data":{"id":10620,"name":{"en_US":"Thorium Ore"}}},
                  {"data":{"id":77,"name":{"en_US":"Thorium Ore"}}},
                  {"data":{"id":2589,"name":{"en_US":"Heavy Thorium Ore"}}}
                ]}
                """,
            ListingsJson = bothListed ? Listings : Listings.Replace("\"id\":77", "\"id\":78", StringComparison.Ordinal),
        };

        var quote = await fixture.QuoteAsync("Thorium Ore");

        Assert.Equal(status, quote.Status);
        if (bothListed)
        {
            Assert.Equal([10620, 77], quote.Choices!.Select(choice => choice.Id));
            Assert.Null(quote.LowestUnitPrice);
        }
        else
        {
            Assert.Equal(10620, quote.Item?.Id);
            Assert.Equal("20s 42c", quote.LowestUnitPrice);
        }
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("0", null)]
    [InlineData("9999999999999999", null)]
    [InlineData("Thorium Ore", 0)]
    [InlineData("Thorium Ore", 10001)]
    public async Task InvalidRequestsAndForeignGuildsDoNotAccessBlizzard(string query, int? quantity)
    {
        using var fixture = new Fixture();
        Assert.Equal("invalid_query", (await fixture.QuoteAsync(query, quantity)).Status);
        Assert.False(fixture.Lookup.IsAvailableFor(51));
        Assert.Equal("unavailable", (await fixture.Lookup.QuoteAsync(51, "Thorium Ore", 20, fixture.Token)).Status);
        Assert.Empty(fixture.Requests);
    }

    [Theory]
    [InlineData("{\"id\":999,\"auctions\":[]}")]
    [InlineData("{\"id\":114}")]
    [InlineData("{\"id\":114,\"auctions\":[{\"item\":{\"id\":10620},\"quantity\":0,\"buyout\":1}]}")]
    public async Task AWrongOrMalformedFeedIsNotTreatedAsANoBuyoutsMarket(string listings)
    {
        using var fixture = new Fixture { ListingsJson = listings };
        var quote = await fixture.QuoteAsync("10620");
        Assert.Equal("unavailable", quote.Status);
        Assert.Null(quote.LowestUnitCopper);
        Assert.Null(quote.SnapshotAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1441)]
    [InlineData(10)]
    public async Task MissingOrUnusablePublicationTimesAreNotReplacedWithTheFetchTime(int? minutesFromNow)
    {
        using var fixture = new Fixture();
        fixture.CommoditiesAt = minutesFromNow is { } minutes ? fixture.Clock.Now.AddMinutes(minutes) : null;
        var quote = await fixture.QuoteAsync("10620");
        Assert.Equal("unavailable", quote.Status);
        Assert.Null(quote.SnapshotAtUtc);
    }

    [Fact]
    public async Task AValidOlderSnapshotIsLabelledStale()
    {
        using var fixture = new Fixture();
        fixture.ListingsAt = fixture.CommoditiesAt = fixture.Clock.Now.AddHours(-3);
        var quote = await fixture.QuoteAsync("10620");
        Assert.Equal("ok", quote.Status);
        Assert.Equal(fixture.CommoditiesAt, quote.SnapshotAtUtc);
        Assert.True(quote.IsStale);
    }

    [Fact]
    public async Task AFailedHalfOfTheMarketDoesNotProduceAPartialQuoteAndHonoursRetryAfter()
    {
        using var fixture = new Fixture { CommodityStatus = HttpStatusCode.TooManyRequests };
        var missing = await fixture.QuoteAsync("2589");
        Assert.Equal("unavailable", missing.Status);
        Assert.Null(missing.LowestUnitCopper);
        var fetched = fixture.Requests.Count;
        fixture.CommodityStatus = HttpStatusCode.OK;
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(2);
        Assert.Equal("unavailable", (await fixture.QuoteAsync("2589")).Status);
        Assert.Equal(fetched, fixture.Requests.Count);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(2);
        Assert.Equal("ok", (await fixture.QuoteAsync("2589")).Status);
        Assert.Equal(fetched + 1, fixture.Requests.Count);
    }

    [Fact]
    public async Task CallerCancellationPropagatesAndDoesNotBackOffTheNextQuote()
    {
        using var fixture = new Fixture();
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        fixture.BeforeResponse = token =>
        {
            caller.Cancel();
            return Task.Delay(Timeout.InfiniteTimeSpan, token);
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Lookup.QuoteAsync(50, "10620", null, caller.Token));
        fixture.BeforeResponse = null;
        Assert.Equal("ok", (await fixture.QuoteAsync("10620")).Status);
    }

    private sealed class Fixture : IDisposable
    {
        public TestClock Clock { get; } = new();
        public BlizzardAuctionPriceLookup Lookup { get; }
        public List<Request> Requests { get; } = [];
        public string ListingsJson { get; set; } = Listings;
        public string CommoditiesJson { get; set; } = Commodities;
        public string SearchJson { get; set; } = Search;
        public string RetailSearchJson { get; set; } = EmptySearch;
        public DateTimeOffset? ListingsAt { get; set; } = new DateTimeOffset(2026, 11, 25, 1, 23, 1, TimeSpan.Zero);
        public DateTimeOffset? CommoditiesAt { get; set; } = new DateTimeOffset(2026, 11, 25, 1, 23, 0, TimeSpan.Zero);
        public HttpStatusCode CommodityStatus { get; set; } = HttpStatusCode.OK;
        public Func<CancellationToken, Task>? BeforeResponse { get; set; }
        public bool Revalidate { get; set; }
        public CancellationToken Token => TestContext.Current.CancellationToken;

        public Fixture() =>
            Lookup = new(
                new HttpClient(new Handler(SendAsync)),
                new BlizzardRosterOptions
                {
                    ClientId = "test-client",
                    ClientSecret = "test-secret",
                    AuctionPricesEnabled = true,
                },
                50,
                Clock
            );

        public Task<AuctionPriceQuote> QuoteAsync(string item, int? quantity = null) => Lookup.QuoteAsync(50, item, quantity, Token);

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var uri = request.RequestUri!;
            Requests.Add(new(uri, request.Headers.Authorization?.Scheme, request.Headers.IfModifiedSince));
            if (BeforeResponse is not null)
                await BeforeResponse(token);
            if (uri.Host == "oauth.battle.net")
            {
                Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
                return new(HttpStatusCode.OK) { Content = new StringContent("""{"access_token":"test-token","expires_in":86400}""") };
            }
            Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
            if (Revalidate && request.Headers.IfModifiedSince.HasValue)
                return new(HttpStatusCode.NotModified);
            if (uri.AbsolutePath == "/data/wow/search/item")
                return Response(
                    HttpStatusCode.OK,
                    uri.Query.Contains("namespace=static-eu&", StringComparison.Ordinal) ? RetailSearchJson : SearchJson,
                    null
                );
            return uri.AbsolutePath.EndsWith("/commodities", StringComparison.Ordinal)
                ? Response(CommodityStatus, CommoditiesJson, CommoditiesAt)
                : Response(HttpStatusCode.OK, ListingsJson, ListingsAt);
        }

        private static HttpResponseMessage Response(HttpStatusCode status, string body, DateTimeOffset? lastModified)
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body) };
            response.Content.Headers.LastModified = lastModified;
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromDays(1) };
            if (status == HttpStatusCode.TooManyRequests)
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(3));
            return response;
        }

        public void Dispose() => Lookup.Dispose();
    }

    private sealed record Request(Uri Uri, string? Scheme, DateTimeOffset? ModifiedSince);

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 25, 1, 31, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
