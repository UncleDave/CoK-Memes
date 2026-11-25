namespace ChampionsOfKhazad.Bot.Roster;

public sealed record AuctionPriceQuote(string Status)
{
    public string? Market { get; init; }
    public AuctionItem? Item { get; init; }
    public AuctionItem[]? Choices { get; init; }
    public decimal? LowestUnitCopper { get; init; }
    public string? LowestUnitPrice { get; init; }
    public int? Quantity { get; init; }
    public string? EstimatedTotal { get; init; }
    public DateTimeOffset? SnapshotAtUtc { get; init; }
    public long? SnapshotUnixSeconds => SnapshotAtUtc?.ToUnixTimeSeconds();
    public bool IsStale { get; init; }
}
