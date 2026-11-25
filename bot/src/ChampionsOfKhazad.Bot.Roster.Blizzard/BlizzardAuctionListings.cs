using System.Collections.ObjectModel;
using System.Text.Json;

namespace ChampionsOfKhazad.Bot.Roster.Blizzard;

internal static class BlizzardAuctionListings
{
    private const int MaximumBodyBytes = 64 * 1024 * 1024;
    private const int MaximumListings = 600_000;
    private static readonly JsonSerializerOptions WireOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, MaxDepth = 24 };

    public static async Task<Dictionary<int, decimal>> ReadAsync(
        HttpContent content,
        int houseId,
        bool commodities,
        CancellationToken cancellationToken
    )
    {
        await using var body = await content.ReadAsStreamAsync(cancellationToken);
        using var limited = new BlizzardBoundedReadStream(body, MaximumBodyBytes);
        var document = await JsonSerializer.DeserializeAsync<Document>(limited, WireOptions, cancellationToken);
        if (document?.Auctions is not { } listings || (!commodities && document.Id != houseId))
            throw new InvalidDataException("Expected a complete auction document for the configured market.");

        var prices = new Dictionary<int, decimal>();
        foreach (var listing in listings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (listing.Item.Id <= 0 || listing.Quantity <= 0)
                throw new InvalidDataException("Auction listing is missing an item or quantity.");
            var amount = commodities ? listing.UnitPrice : listing.Buyout;
            if (!commodities && amount is null or 0)
                continue; // A bid is not a buyout.
            if (amount is null or <= 0)
                throw new InvalidDataException("Auction listing has an invalid price.");

            var unit = commodities ? amount.Value : (decimal)amount.Value / listing.Quantity;
            if (!prices.TryGetValue(listing.Item.Id, out var best) || unit < best)
                prices[listing.Item.Id] = unit;
        }
        return prices;
    }

    private sealed record Document(int Id, ListingCollection? Auctions);

    private readonly record struct Listing(ItemReference Item, int Quantity, long? Buyout, long? UnitPrice);

    private readonly record struct ItemReference(int Id);

    // Bound the row count during deserialization, including unusually small malformed entries.
    private sealed class ListingCollection : Collection<Listing>
    {
        protected override void InsertItem(int index, Listing item)
        {
            if (Count >= MaximumListings)
                throw new InvalidDataException("Auction snapshot exceeded the listing limit.");
            base.InsertItem(index, item);
        }
    }
}
