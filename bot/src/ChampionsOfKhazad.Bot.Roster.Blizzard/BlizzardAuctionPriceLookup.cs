using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot.Roster.Blizzard;

public sealed class BlizzardAuctionPriceLookup(
    HttpClient http,
    BlizzardRosterOptions options,
    ulong guildId,
    TimeProvider clock,
    ILogger<BlizzardAuctionPriceLookup>? logger = null
) : IAuctionPriceLookup, IDisposable
{
    private const int HouseId = 114;
    private const string Market = "EU Normal Horde";
    private const string HousePath = "/data/wow/ruleset/2/auctions/114";
    private const int SearchPageSize = 50;
    private const int MaximumSearchesCached = 128;
    private static readonly TimeSpan FeedLifetime = TimeSpan.FromHours(1);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, ItemResolution> _catalogue = new(StringComparer.OrdinalIgnoreCase);
    private BlizzardAppToken? _appToken;
    private Feed? _listings;
    private Feed? _commodities;
    private DateTimeOffset _blockedUntil;

    public bool IsAvailableFor(ulong requestedGuildId) =>
        options.AuctionPricesEnabled && options.Region == "eu" && guildId != 0 && requestedGuildId == guildId;

    public async Task<AuctionPriceQuote> QuoteAsync(ulong requestedGuildId, string item, int? quantity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsAvailableFor(requestedGuildId))
            return new("unavailable");
        if (item?.Trim() is not { Length: > 0 and <= 120 } query || query.Any(char.IsControl) || quantity is < 1 or > 10_000)
            return new("invalid_query") { Market = Market };
        var itemId = 0;
        if (query.All(char.IsAsciiDigit) && (!int.TryParse(query, NumberStyles.None, CultureInfo.InvariantCulture, out itemId) || itemId <= 0))
            return new("invalid_query") { Market = Market };

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var token = linked.Token;
        try
        {
            await _gate.WaitAsync(token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }

        try
        {
            if (clock.GetUtcNow() < _blockedUntil)
                return Unavailable();
            AuctionItem resolved;
            if (itemId > 0)
                resolved = new(itemId, FormattableString.Invariant($"Item #{itemId}"));
            else
            {
                var match = await FindItemAsync(query, token);
                if (match.Item is not { } found)
                    return new(match.Choices.Length == 0 ? "item_not_found" : "ambiguous_item") { Market = Market, Choices = match.Choices };
                resolved = found;
            }

            var listings = await GetFeedAsync(commodities: false, token);
            var commodities = await GetFeedAsync(commodities: true, token);
            return Price(resolved, quantity, listings, commodities, clock.GetUtcNow());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            BackOffUntil(clock.GetUtcNow().AddMinutes(1));
            logger?.LogDebug("Blizzard auction lookup unavailable; status {StatusCode}", (exception as BlizzardApiException)?.StatusCode);
            return Unavailable();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ItemResolution> FindItemAsync(string query, CancellationToken cancellationToken)
    {
        if (_catalogue.TryGetValue(query, out var cached) && clock.GetUtcNow() < cached.Expires)
            return cached;
        using var response = await GetAsync(
            $"/data/wow/search/item?namespace=static-forever-eu&locale=en_GB&name.en_US={Uri.EscapeDataString(query)}&_page=1&_pageSize={SearchPageSize}",
            null,
            cancellationToken
        );
        using var document = await ReadJsonAsync(response.Content, 2 * 1024 * 1024, cancellationToken);
        var root = document.RootElement;
        var matches = new Dictionary<int, AuctionItem>();
        if (root.TryGetProperty("results", out var results))
        {
            if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() > SearchPageSize)
                throw new InvalidDataException("Expected a bounded item search page.");
            foreach (var entry in results.EnumerateArray())
            {
                var data = entry.GetProperty("data");
                var id = data.GetProperty("id").GetInt32();
                var name = data.GetProperty("name").GetProperty("en_US").GetString();
                if (id <= 0 || name is not { Length: > 0 and <= 120 } || name.Any(char.IsControl))
                    throw new InvalidDataException("Item search returned an invalid item.");
                matches.TryAdd(id, new(id, name));
            }
        }
        else if (!root.TryGetProperty("pageCount", out var pageCount) || pageCount.GetInt32() != 0)
            throw new InvalidDataException("Item search did not return results or an empty page count.");

        var exact = matches.Values.Where(hit => hit.Name.Equals(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        var choices = exact.Length > 0 ? exact : matches.Values.Where(hit => hit.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        var now = clock.GetUtcNow();
        var result = new ItemResolution(
            exact.Length == 1 ? exact[0] : null,
            exact.Length == 1 ? [] : choices.Take(6).ToArray(),
            Expires(response, now)
        );
        if (_catalogue.Count >= MaximumSearchesCached)
            _catalogue.Clear();
        if (result.Expires > now)
            _catalogue[query] = result;
        return result;
    }

    private async Task<Feed> GetFeedAsync(bool commodities, CancellationToken cancellationToken)
    {
        var cached = commodities ? _commodities : _listings;
        if (cached is not null && clock.GetUtcNow() < cached.Expires)
            return cached;
        var path = commodities ? HousePath + "/commodities" : HousePath;
        using var response = await GetAsync($"{path}?namespace=dynamic-forever-eu&locale=en_GB", cached?.SnapshotAt, cancellationToken);
        var now = clock.GetUtcNow();
        var expires = Expires(response, now);
        Feed feed;
        if (response.StatusCode == HttpStatusCode.NotModified && cached is not null)
            feed = cached with { Expires = expires };
        else
        {
            var snapshotAt = response.Content.Headers.LastModified?.ToUniversalTime();
            if (snapshotAt is null || snapshotAt > now.AddMinutes(5) || snapshotAt < now.AddDays(-1))
                throw new InvalidDataException("Auction snapshot has no usable publication timestamp.");
            var prices = await BlizzardAuctionListings.ReadAsync(response.Content, HouseId, commodities, cancellationToken);
            feed = new(prices, snapshotAt.Value, expires);
        }

        var retain = response.Headers.CacheControl?.NoStore == true ? null : feed;
        if (commodities)
            _commodities = retain;
        else
            _listings = retain;
        return feed;
    }

    private static AuctionPriceQuote Price(AuctionItem item, int? quantity, Feed listings, Feed commodities, DateTimeOffset now)
    {
        var oldest = listings.SnapshotAt < commodities.SnapshotAt ? listings.SnapshotAt : commodities.SnapshotAt;
        if (oldest < now.AddDays(-1) || (listings.SnapshotAt - commodities.SnapshotAt).Duration() > TimeSpan.FromHours(2))
            throw new InvalidDataException("Auction feeds are too old or describe different market windows.");
        var itemPrice = listings.Prices.GetValueOrDefault(item.Id);
        var commodityPrice = commodities.Prices.GetValueOrDefault(item.Id);
        if (itemPrice == 0 && commodityPrice == 0)
            return new("no_buyouts")
            {
                Market = Market,
                Item = item,
                SnapshotAtUtc = oldest,
                IsStale = oldest < now.AddHours(-2),
            };

        var useCommodities = itemPrice == 0 || (commodityPrice > 0 && commodityPrice <= itemPrice);
        var lowest = useCommodities ? commodityPrice : itemPrice;
        return new("ok")
        {
            Market = Market,
            Item = item,
            Quantity = quantity,
            LowestUnitCopper = lowest,
            LowestUnitPrice = FormatCopper(lowest),
            EstimatedTotal = quantity is > 1
                ? FormatCopper(Math.Max(1, decimal.Round(lowest * quantity.Value, 0, MidpointRounding.AwayFromZero)))
                : null,
            SnapshotAtUtc = useCommodities ? commodities.SnapshotAt : listings.SnapshotAt,
            IsStale = oldest < now.AddHours(-2),
        };
    }

    private static string FormatCopper(decimal value)
    {
        var copper = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        if (copper == 0)
            return "<0.01c";
        var parts = new List<string>(3);
        if (decimal.Truncate(copper / 10_000) is > 0 and var gold)
            parts.Add(FormattableString.Invariant($"{gold}g"));
        if (decimal.Truncate(copper % 10_000 / 100) is > 0 and var silver)
            parts.Add(FormattableString.Invariant($"{silver}s"));
        if (copper % 100 is > 0 and var change)
            parts.Add(FormattableString.Invariant($"{change:0.##}c"));
        return string.Join(' ', parts);
    }

    private async Task<HttpResponseMessage> GetAsync(string pathAndQuery, DateTimeOffset? modifiedSince, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var appToken = await GetTokenAsync(cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{options.ApiHost}{pathAndQuery}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", appToken.Value);
            request.Headers.IfModifiedSince = modifiedSince;
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode || (response.StatusCode == HttpStatusCode.NotModified && modifiedSince.HasValue))
                return response;
            using (response)
            {
                if (response.StatusCode != HttpStatusCode.Unauthorized || attempt != 0)
                    throw UpstreamFailure(response);
                _appToken = null;
            }
        }
    }

    private async Task<BlizzardAppToken> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_appToken is { } cached && clock.GetUtcNow() < cached.RenewAtUtc)
            return cached;
        using var request = BlizzardAppToken.CreateRequest(options);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw UpstreamFailure(response);
        using var document = await ReadJsonAsync(response.Content, 64 * 1024, cancellationToken);
        return _appToken = BlizzardAppToken.Parse(document.RootElement, clock.GetUtcNow());
    }

    private BlizzardApiException UpstreamFailure(HttpResponseMessage response)
    {
        var retry = response.Headers.RetryAfter;
        if (retry?.Date is { } date)
            BackOffUntil(date);
        else if (retry?.Delta is { } delta)
            BackOffUntil(clock.GetUtcNow().Add(delta));
        return new("Blizzard auction API request failed.", response.StatusCode);
    }

    private void BackOffUntil(DateTimeOffset until)
    {
        if (until > _blockedUntil)
            _blockedUntil = until;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpContent content, int byteLimit, CancellationToken cancellationToken)
    {
        await using var body = await content.ReadAsStreamAsync(cancellationToken);
        using var limited = new BlizzardBoundedReadStream(body, byteLimit);
        return await JsonDocument.ParseAsync(limited, cancellationToken: cancellationToken);
    }

    private static DateTimeOffset Expires(HttpResponseMessage response, DateTimeOffset now)
    {
        var cache = response.Headers.CacheControl;
        if (cache?.NoStore == true || cache?.NoCache == true)
            return now;
        var seconds = (cache?.MaxAge ?? FeedLifetime).TotalSeconds - (response.Headers.Age?.TotalSeconds ?? 0);
        return now.AddSeconds(Math.Clamp(seconds, 0, FeedLifetime.TotalSeconds));
    }

    private static AuctionPriceQuote Unavailable() => new("unavailable") { Market = Market };

    public void Dispose()
    {
        http.Dispose();
        _gate.Dispose();
    }

    private sealed record ItemResolution(AuctionItem? Item, AuctionItem[] Choices, DateTimeOffset Expires);

    private sealed record Feed(IReadOnlyDictionary<int, decimal> Prices, DateTimeOffset SnapshotAt, DateTimeOffset Expires);
}
