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
  owner ID must match the requested character. Optional `known_recipes` arrays on
  each profession/tier supply recipe IDs and names for guild crafter lookup.

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

A 404 from the membership request specifically becomes expected source
unavailability: it is logged at Information with no exception stack, below the
Discord Serilog sink's Error threshold. Polling continues and prior membership is
retained. This does not demote OAuth failures, missing static-class resources,
invalid response schemas, or notification delivery failures. The application does
not apply a broad 404/message filter to the logging sink.

## Shared feature and persistence

- `ChampionsOfKhazad.Bot.Roster` owns normalization, validation, refresh, queries,
  rendering, and progression comparison, without HTTP/Discord/Mongo/AI dependencies.
- `ChampionsOfKhazad.Bot.Roster.Blizzard` owns HTTP/OAuth, the assumed wire schema,
  class lookup, and the live source.
- `ChampionsOfKhazad.Bot.Roster.Mongo` stores current members/high-water marks in
  `guildRosters` and pending/sent notices in `guildRosterNotices`.
- One host worker checks due refresh/delivery work every ten seconds while Discord
  is ready and `BotContext` is available. Readiness is checked again before delivery
  if a refresh spans a disconnect. The configured membership interval is 60 seconds and separate profession
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
class, level/rank, and one `Last checked` date/time. Profession filters accept names,
IDs, or an unambiguous ID:tier. Unknown ranks are not zero-filled. Structured
coverage reports unique affected
characters out of the whole guild (or name-filtered set), with separate counts for
unavailable observations, partial observations, failed refreshes, and unknown ranks
in the requested profession. Counts can overlap, and known complete empty lists
are not counted as missing. This coverage is independent of pagination and minimum
skill filters; it is not a claim that the displayed practitioners lack data. These
counts remain available to Grub for diagnostic questions, not routine message copy.

Level messages use the roster check time for the single date footer. Profession
messages use the oldest known profession check among the displayed rows. Discord's
date/time format (`f`) renders this in the reader's locale, rather than a relative
source age. Check times include failed attempts and HTTP cache hits/revalidation,
not just fresh network downloads. Profession check times are recorded when each
attempt completes; missing historical checks are not replaced with membership
checks. Empty profession results show no fabricated check date. Routine messages
omit coverage breakdowns, source timestamps, and oldest/newest ranges. Failed
checks affecting displayed profession rows still get a short retained-data warning;
the existing roster-refresh failure warning also remains. Grub receives coverage
counts and per-character check/source timestamps for on-demand questions, but its
routine copy follows the same last-checked-only policy. Source timestamps retain
the upstream Last-Modified or original receipt time described above, internally.

Empty, no-match, unavailable,
out-of-range, and failure states stay distinct. Text is bounded/escaped and pings
are disabled.

Normal guild-chat Grub receives `get_guild_leaderboard`, `find_guild_characters`,
`find_guild_profession_members`, and `find_guild_recipe_crafters` over the same scoped queries. DMs, foreign guilds,
followers, and barred customers do not receive these tools. Existing customer
execution guards still apply after a third strike. Roster-specific prompt guidance
is composed only when the tools are actually present, independent of main's newer
Grub voice prompt. Current levels/skills come from roster data, not lore or the web;
tool results are quoted data. Profession ranks cannot establish recipe knowledge;
only the recipe tool's explicit matches do. Neither establishes ownership or event times.

### Recipe lookup

Ask Grub, for example, "Who knows Hot Lion Chops?" or "Who can craft Big Bear Steak?"
The tool searches recorded known recipe names (case-insensitive, full or partial)
or recipe IDs, not item/spell IDs. An exact ID takes precedence over an exact name,
which takes precedence over substring matches. Multiple recipe IDs return
`ambiguous_recipe` with bounded choices; Grub asks which recipe before querying its
ID. Choices and crafters are paginated ten per page. Crafters are unique characters
ordered by full name, not skill rank; an unknown rank does not exclude an explicitly
known recipe. Secondary professions are included.

Collection reuses the existing profession requests and cache policy. Store only
recipe IDs/names, not returned links or full recipe documents. Missing
`known_recipes` means unknown; `[]` means a known empty list for that skill track.
Malformed lists, duplicate IDs within a track, or more than 5,000 recipes per track
fail the optional profession refresh and preserve prior data. Older observations
cannot overwrite newer recipes. A newer valid response omitting the list makes
that track unknown; it does not establish that recipes were unlearned. Departed
characters stop appearing with the normal complete-membership replacement.

Existing Mongo documents need no migration: absent recipe fields deserialize as
unknown until the next successful profession refresh. Ordinary roster tools omit
recipe arrays to keep their responses small. Recipe results include only the
selected recipe/crafter page, per-crafter profession source/check timestamps and
failure flags, the roster-refresh flag, and whole-guild coverage counts. Coverage
counts unavailable professions, partial observations/missing recipe lists, and
failed checks separately, with unique affected characters; known empty lists are
not missing coverage. `no_match` is no recorded match, not proof that nobody knows
the recipe. Keep coverage diagnostic detail out of routine replies, but preserve
actual refresh failures. Use one last-checked date from the oldest known profession
check among displayed crafters; never substitute a membership check.

Known recipes do not prove materials, current availability, willingness to craft,
or Discord ownership. Gathering skills can also have entries named as recipes;
do not invent a crafted output. Item-to-recipe mapping, ingredient/cost lookup,
static recipe catalogues, slash commands, and recipe announcements are not added.

Gateway notifications are queued only after Discord is ready with a non-null
`BotContext`. Messages/member departures received before that are not sent to
context-dependent scoped handlers; early slash commands/buttons get a private retry
response. Disconnects stop new queue submissions without clearing the previous
context, so already accepted notifications can still drain safely. This prevents
pre-Ready handler construction from raising `BotContext is not available`.

## Milestones

Destinations: Development `1092927596938924054`, Production `1567646266689851402`.
The sender validates that the destination is a text channel in the configured
Discord guild and disables all mentions.

Character thresholds are every ten levels plus `LevelCap` (default 60); profession
thresholds default to 75/150/225/300, always include 300, and include secondary professions. Crossing
queues only the highest newly crossed threshold per track. Initial/new character
and profession observations baseline silently. Unknowns/regressions do not reset
high-water marks; a trained maximum changing alone does not trigger anything.

### Celebrations

The accepted [milestone design](guild-roster.md#milestone-notifications) adds:

- A green celebration embed for each newly crossed level 60 or profession rank 300,
  with a short authored Grub commendation. Ordinary milestones retain plain text.
- Gold recognition for the first recorded level 60 in the guild, the first recorded
  level 60 of each class, and the first recorded 300 in each profession/skill track.
  Multiple qualifying arrivals in one refresh receive joint recognition, never an
  ordering inferred from names or response order. The API does not establish actual
  achievement times, hence “first recorded” rather than an historical race claim.
- Shared celebrations when every class has a current level-60 character,
  or every profession has a current character with a known rank of 300.
- Shared milestones for 5, 10, and 20 current characters at 60, once each. These are
  distinct characters, including alts, not Discord members; there are no per-class
  or profession-count milestones. A jump over multiple counts announces only the
  highest newly crossed count and consumes the lower thresholds.

The complete class set is Druid, Hunter, Mage, **Paladin**, Priest, Rogue, Shaman,
Warlock, and Warrior: Horde has Paladins in Forever. The complete profession set is
Alchemy (171), Blacksmithing (164), Enchanting (333), Engineering (202), Herbalism
(182), Leatherworking (165), Mining (186), Skinning (393), Tailoring (197), Cooking
(185), Fishing (356), and First Aid (129). The static catalogue's test profession
is not a completion requirement. These required sets are explicit; never infer
“every” from whichever categories happen to be present in a roster. Unknown ranks
do not count; a missing/failed section alone
does not erase previously known profession observations. Firsts retain skill-track
separation; completion requires a qualifying known track for each required ID.

Firsts are attached to the personal notice, not posted again separately. Personal
and shared honours from one refresh are packed into the same embed, with shared
honours leading and gold firsts taking presentation priority. Purple identifies a
shared milestone without firsts; green identifies ordinary cap celebrations. Split
only when needed to fit Discord limits, with each persisted notice included once.
Separate observations are not merged into a supposed joint first. Names, class and
profession data are escaped; embeds and plain notices both disable all mentions.

`RosterState.Celebrations` stores consumed first/completion/count keys alongside
existing per-character high-water marks. Already achieved keys are silently seeded
on initial collection and when upgrading an older document without this field;
existing pending notices still deserialize and deliver as plain text. Migration
uses the previous accepted roster and historical high-water marks before evaluating
new crossings, so a genuine new crossing during upgrade can still celebrate. A
legacy departed capped character has no stored class: conservatively consume the
class firsts rather than fabricate a successor as the first.

Newly seen/rejoining capped characters and first-known rank-300 skills baseline
silently and consume applicable keys. A collective notice requires a genuine new
cap crossing in that refresh; joiners/data discovery alone never trigger a
retrospective burst. If a silently baselined arrival shares a previously unclaimed
first category with a genuine crossing, suppress that first claim rather than
invent an order. Keys remain consumed through regressions, departures and restarts;
completion/count calculations use current membership, not accumulated departed
characters. Disabled delivery advances the baseline without queuing catch-up posts.
An explicit source change uses its existing separate roster scope and seeds anew.

### Delivery

Pending notices are saved before sending, then marked sent with Discord message ID
and UTC time. Failed sends remain pending. One worker and unique keys prevent
ordinary duplicates; this remains best effort, not exactly-once. A crash between
state and notice writes can miss a milestone, and a send accepted before a failed
sent-status write can duplicate on retry. No distributed leases, multi-document
transactions, history archive, or catch-up throttling have been added.

Departure cleanup removes only unsent personal notices. Shared guild accomplishments
are independent of an individual contributor and remain deliverable after departures.
Synthetic regression tests cover celebration detection, migration, restarts, combined
Discord payloads, mentions, and size limits. Live Discord presentation is not yet verified.

### Diagnosing a delayed level announcement

Levels come only from the membership roster; professions come from independently
cached character-profession responses. The live guild roster advertises
`max-age=10800` (three hours). Checking every minute does not download a new roster
while that HTTP cache entry remains fresh. Professions can therefore announce new
skills while `/leaderboard` still shows a character's previous level. Its `Last
checked` footer is not evidence of a fresh upstream download.

If a reported 60 still appears as 59 in `/leaderboard`, first distinguish a cached
level from a delivery failure. A separate diagnostic request can already see 60
while the running bot legitimately retains its earlier cached roster. Read the
roster `cache_hit`/`fetched` diagnostics and `CacheExpiresUtc` to establish the bot's
actual refresh deadline; a new diagnostic process cannot reveal that deadline.
The character profile may also be newer, but is not currently an input to level
detection. Do not reset progression, silently change sources, bypass upstream cache
policy, or manually replay a milestone merely because the endpoints differ.

An end-to-end fake-HTTP regression covers both ordinary profession notices and
rank-300 firsts arriving during a three-hour cached level-59 roster. Neither consumes
the first-60 honour. After roster expiry, an accepted 60 produces and delivers the
combined guild/class first celebration exactly once in the normal polling sequence.
If the accepted roster already says 60 but no announcement appears, inspect the
stored high-water mark and pending/sent notice before diagnosing an HTTP delay.

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

Profession scan summaries are logged at Information when a separate-profession
scan runs: attempted, unavailable, failed, partial, and older-response counts,
duration, and retained skill source-timestamp bounds. A newer retained observation
is not overwritten by an older response. For detailed troubleshooting, enable
Debug for `ChampionsOfKhazad.Bot.Roster` and
`ChampionsOfKhazad.Bot.Roster.Blizzard`. These logs distinguish fetched documents,
local cache hits, 304 revalidation, HTTP status failures, shared backoff skips, and
invalid-data/source-failure categories. Cache diagnostics include numeric max-age
and Age values, expiry, and source timestamp. They do not log response bodies,
tokens, credentials, character URLs/names, or raw exception details. Numeric
character IDs may appear in failed-refresh diagnostics. Existing roster and
notification Error-level logging is unchanged; profession diagnostics do not send
routine Discord alerts. Polling intervals and cache/backoff behavior are unchanged.

Emergency stop: set `GuildRoster__Enabled=false` and restart the bot. This stops
collection, tool exposure, leaderboard registration, and delivery without deleting
stored data or disabling unrelated features. This is a restart-time setting, not
a live toggle; it is a rollback option, not the default rollout plan.
