namespace ChampionsOfKhazad.Bot.Roster;

public interface IAuctionPriceLookup
{
    bool IsAvailableFor(ulong guildId);
    Task<AuctionPriceQuote> QuoteAsync(ulong guildId, string item, int? quantity, CancellationToken cancellationToken);
}
