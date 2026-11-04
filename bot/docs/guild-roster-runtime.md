# Guild roster implementation

## Live rollout

The bot now uses a live Blizzard `IGuildRosterSource`, enabled in the checked-in
settings for **EU / Crossroads Mega Chippy** (`crossroads-mega-chippy`). There is
no runtime mock selector, fixture fallback, or bundled fake roster. The old file
source and its fictional fixtures live only in the backend test project.

This implements the administrator's 4 November decision to prepare the real feature
for launch using the expected API contract and correct it against real responses
after launch. It is **not** a claim that Blizzard has confirmed that contract.
The [research checkpoint](forever-api-research.md) records what was actually observed.
Setup is in the [README](../README.md#guild-roster); accepted scope is in the
[design plan](guild-roster.md).

## Expected routes and schema

All requests use the fixed regional Blizzard HTTPS host, `profile-forever-{region}`
or `static-forever-{region}`, and `locale=en_GB`. Redirects and cookies are disabled.
No player-facing realm selection or realm-index success is required.

Default assumptions:

- Membership: `/data/wow/guild/{guild}/roster`, with `guild.id`, `guild.name`, and
  a complete `members[].character` array. The guild name must match configuration;
  the refresh service also rejects a changed stable guild ID after its baseline.
- Characters: positive numeric `id`, first name in `name`, last name in `surname`,
  integer `level`, and `playable_class`. Class names can come from that object or
  the live static class index when only a class ID is present. Names are never
  split from a full display name or adapted from the old fictional fixture fields.
- Professions: the character's returned `key.href` plus `/professions`, or
  `/profile/wow/character/{first}-{last}/professions` when no link is supplied.
  Returned links must point to the same regional HTTPS character resources. Their
  query parameters are discarded; our namespace/locale are applied explicitly.
- Profession responses use `primaries`/`secondaries`, `profession.id/name`, optional
  `tiers[].tier.id`, and nullable `skill_points`/`max_skill_points`. A supplied
  owner ID must match the requested character. Recipe data is ignored.

The two path templates are configurable so a launch-day routing correction does not
require a domain rewrite. Templates are paths, not URLs or arbitrary query strings.
They cannot redirect app tokens to another host. The roster template requires
`{guild}`; the profession template requires `{first}` and `{last}`. Internal routing
segments may be supplied only if actual responses require them; do not impose
Classic realm/name assumptions on Forever.

Membership is bounded to 5,000 entries and 2 MiB of JSON. Missing required fields,
duplicate IDs, invalid levels/classes, a mismatched guild, explicit pagination, or a
declared member count that disagrees with the array rejects the response. In the
absence of pagination metadata, the expected WoW-style guild roster array is treated
as complete. This assumption needs a real launch-day fixture/completeness check;
the adapter never manufactures an empty roster from 403/404 or malformed JSON.

Professions are optional and independently timestamped. A 404, missing sections,
missing rank, and explicit complete empty arrays remain distinct. A profession
failure retains prior same-source knowledge and marks failed refresh coverage;
it cannot change membership/levels. A complete level roster can operate without
profession support. Static profession catalogues are not character skill data.

## Authentication, caching, and failures

The source obtains an app token from `https://oauth.battle.net/token` with client
credentials, caches it until shortly before expiry, and renews once on a resource
401. No player login, account profile, or `wow.profile` permission is required.
Credentials come from configuration, never source code or committed settings.
Development can fall back to the existing `cok-forever-api-probe` user-secrets store;
explicit bot configuration wins. Production receives the client ID and secret from
Pulumi configuration and Azure Container App settings. Nothing copies local secrets
to production automatically.

Requests are serialized through a shared client/gate. Each has a 30-second timeout;
JSON bodies are bounded to 2 MiB. A bounded in-memory response cache (up to 256 entries
and 16 MiB of body data) honours `max-age`, `Age`, `no-cache`, `no-store`, ETag and
Last-Modified. A 304 retains the previous observation timestamp. Without a source
Last-Modified, the original successful receipt time is used. A cache hit or successful
check does not relabel an old API document as newly observed gameplay.

429/5xx/authentication failures back off the shared client using Retry-After, or 60
seconds when absent. Other transport failures also back off for 60 seconds. Apart
from one token renewal on 401 there are no immediate HTTP retries. Error messages
contain status/category, not response bodies, tokens, secrets, or raw inner transport
exceptions. The worker records a failed refresh while preserving the previous
accepted roster. Until the first valid response, queries report no usable data or
refresh failure, not an empty guild. There is no switch to fictional data on failure.

## Shared feature and persistence

- `ChampionsOfKhazad.Bot.Roster` owns normalization, validation, refresh, queries,
  rendering, and progression comparison, without HTTP/Discord/Mongo/AI dependencies.
- `ChampionsOfKhazad.Bot.Roster.Blizzard` owns HTTP/OAuth, the assumed wire schema,
  class lookup, and the live source.
- `ChampionsOfKhazad.Bot.Roster.Mongo` stores current members/high-water marks in
  `guildRosters` and pending/sent notices in `guildRosterNotices`.
- One host worker checks due refresh/delivery work every ten seconds while Discord
  is ready. The configured membership interval is 60 seconds and separate profession
  interval 600 seconds, subject to upstream cache/backoff. Commands and Grub read
  the shared database-backed cache rather than starting individual scans.
- Live source identity contains region, guild slug, and route templates. A source
  switch is explicit and silently baselines its own dataset; old test/mock progress
  cannot leak into live data. There is no cross-source merging.

Only accepted complete membership replaces members. Departed/deleted characters
leave current queries, stop profession polling, and lose pending unsent notices.
A failed lookup is not a departure. Numeric IDs preserve progression through renames;
different IDs reusing a name remain distinct. Rejoins seed silently. Name-only
identity support in the shared model is not used to hide missing live character IDs.

## Discord and Grub

`/leaderboard [profession] [page]` is guild-registered while enabled. It defers before
queued work and returns ten characters per page with shared ranks, full names,
class, level/rank, and observation age. Profession filters accept names, IDs, or an
unambiguous ID:tier. Unknown ranks are not zero-filled; incomplete profession
coverage and failed refreshes remain visible. Empty, no-match, unavailable,
out-of-range, and failure states stay distinct. Text is bounded/escaped and pings
are disabled.

Normal guild-chat Grub receives `get_guild_leaderboard`, `find_guild_characters`, and
`find_guild_profession_members` over the same scoped queries. DMs, foreign guilds,
followers, and barred customers do not receive these tools. Existing customer
execution guards still apply after a third strike. Roster-specific prompt guidance
is composed only when the tools are actually present, independent of main's newer
Grub voice prompt. Current levels/skills come from roster data, not lore or the web;
tool results are quoted data and cannot establish recipes, ownership, or event times.

## Milestones

Destinations: Development `1092927596938924054`, Production `1567646266689851402`.
The sender validates that the destination is a text channel in the configured
Discord guild and disables all mentions.

Character thresholds are every ten levels plus optional `LevelCap`; profession
thresholds default to 75/150/225/300 and include secondary professions. Crossing
queues only the highest newly crossed threshold per track. Initial/new character
and profession observations baseline silently. Unknowns/regressions do not reset
high-water marks; a trained maximum changing alone does not trigger anything.

Pending notices are saved before sending, then marked sent with Discord message ID
and UTC time. Failed sends remain pending. One worker and unique keys prevent
ordinary duplicates; this remains best effort, not exactly-once. A crash between
state and notice writes can miss a milestone, and a send accepted before a failed
sent-status write can duplicate on retry. No distributed leases, multi-document
transactions, history archive, or catch-up throttling have been added.

## Validation and launch debugging

Backend tests exercise the live HTTP/schema boundaries with fake HTTP responses,
alongside rankings, lifecycle, milestone, Mongo, command, and tool-access behavior.
They do not prove Blizzard will expose the assumed launch schema. Fictional file
fixtures and the old file source are test-only and are not published with the bot.
The real roster Mongo integration test passed against disposable `mongo:8.0` on
4 November; rerun with `COK_ROSTER_MONGO_TEST_CONNECTION` for persistence changes.

The 4 November merge-readiness run passed backend build/tests, addon tests,
formatting, and a fresh Release publish with no fixture directory. A temporary
snapshot combining the working changes with `origin/main` at `c6ffb3b` also built
and passed its backend tests without merging either branch. These checks validate
the implementation and integration, not the still-unpublished guild response.

Before publishing, set the two Pulumi Blizzard credential values, run build/tests/
format checks and a clean Release publish, and preview integration with current
`origin/main`. Preserve main's newer unrelated bot behavior. Production starts the
real source after deployment; it does not require a preliminary mock activation.

After launch, check the first real guild response, field names/IDs, membership
completeness, optional profession coverage, logout/publication timing, and one normal
Discord query. Inspect safe status logs and the independent probe when a route or
schema needs adjustment. Correct path templates or parsing against sanitized real
fixtures without accepting partial membership or fabricating stats. The first
successful snapshot stays a silent baseline, even if availability was delayed.

Emergency stop: set `GuildRoster__Enabled=false` and restart the bot. This stops
collection, tool exposure, leaderboard registration, and delivery without deleting
stored data or disabling unrelated features. This is a restart-time setting, not
a live toggle; it is a rollback option, not the default rollout plan.
