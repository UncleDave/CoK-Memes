# Forever auction prices

Ask Grub in guild chat, for example, “What will 20 Thorium Ore cost?” The lookup
covers **EU Normal Horde**. It quotes the lowest listed buyout per item, the
Blizzard snapshot time, and, if a quantity was supplied, an estimate for that
amount at the same unit price. The estimate isn't a guarantee that enough is
listed at that price or that a seller will split a lot.

Search matches an exact English item name on the first bounded catalogue page.
If the name is ambiguous or only partial, Grub asks which item from up to six
choices. A Blizzard item ID bypasses the catalogue. A request for “a stack”
without a quantity gets a unit price and a question about how many; Blizzard's
item detail does not expose a usable inventory stack size. For example, both
Thorium Ore and Linen Cloth returned `is_stackable: true` and `max_count: 0`
in an authenticated EU check on 25 November 2026. Do not substitute item IDs
for recipe IDs in the separate guild crafter lookup.

“No buyouts” is a result from valid snapshots. A catalogue miss, failed lookup,
or unavailable market is not evidence of an empty auction house. Label older
snapshots without repeating standing implementation details in every reply.
There are no bids, purchases, sales history, alerts, public slash commands, or
background polling. The read-only tool is restricted to normal Grub service in
the configured Discord guild; it isn't offered in DMs, to followers, or to
barred customers. Three lookups are allowed per request.

## Market verification

The configured market uses house **114** through the Normal route (ruleset **2**)
on `eu.api.blizzard.com`. The route alone does not pick the right Horde house:
the Normal-route index returned five houses named `Horde Auction House`.
[Auction House's community mapping](https://auctionhouse.tk.gg/data/#markets)
identifies 114 from an exact in-game Normal scan. On 25 November 2026, our
authenticated EU check found **196,277 listings** and **147,034 commodities**
for that ID, matching the separate [AzerothAuctions Normal Horde
counts](https://azerothauctions.com/forever/eu/houses) at that checkpoint. The
bot reads Blizzard directly; it doesn't call either tracker or merge other
factions/rulesets. Recheck the mapping if Blizzard changes the index.

## Snapshot handling

- The two feeds are `/data/wow/ruleset/2/auctions/114` and its `/commodities`
  child in `dynamic-forever-eu`. Ordinary listing `buyout` is in copper for the
  whole lot, so divide by `quantity`; ignore bid-only listings. Commodity
  `unit_price` is already per item. Return the lower unit price across the two
  successful feeds. Format the result in gold, silver and copper; an estimate
  for more than one unit rounds to a whole copper.
- The feeds were about 19.8 and 12.8 MB uncompressed at the EU checkpoint. The
  auction client reads them one at a time, bounded to 64 MiB of decompressed
  JSON and 600,000 rows per feed. It caches only each item's lowest unit price
  and snapshot metadata in memory, not raw listings, seller data, or history.
  A cold lookup has a 20-second budget. The auction client has its own cache,
  HTTP gate, token and backoff; its larger reads don't use the roster client's
  2 MiB path or its gate. Both use the existing Blizzard app credentials.
- Reuse each compact snapshot for up to one hour, subject to upstream cache
  headers. Conditional requests use the original `Last-Modified`; a 304 does
  not change the snapshot time. The returned time is the source of the quoted
  price (the older of the two for no buyouts). Label results if either feed
  is over two hours old. Missing timestamps, feeds more than two hours apart,
  any feed over 24 hours old, and unpublished or invalid feeds are unavailable
  rather than partly priced. Failed requests back off for at least one minute
  and honour a longer `Retry-After`. Only a resource 401 retries immediately,
  once, with a renewed token.

See [bot setup](../README.md#guild-roster) for the feature switch and credential
configuration.
