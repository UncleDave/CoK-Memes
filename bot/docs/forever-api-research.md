# Forever web API research

## Post-launch check — 7 November 2026

Authenticated EU app-token requests at **03:11 UTC** now return 200 for
`/data/wow/guild/crossroads-mega-chippy` and `/roster`, with **65 roster entries**.
The roster exposes numeric guild ranks, `playable_race`, and faction as well as
the names/classes/levels used by the integration. Both guild documents advertise
`public, max-age=10800`; frequent local checks do not bypass that cache. This
supersedes the pre-launch unavailability observations below, not the remaining
completeness/publication-timing checks.

The community's public sample `daxan-windfury` now returns 200 for the profile and
professions. A focused follow-up verified `known_recipes` under each profession's
`tiers[]`, containing `id`, `name`, and `key` per recipe. Four lists contained
27 entries in total across Skinning, Herbalism, Cooking, and Fishing. Example
Cooking entries are recipe **1901 / Big Bear Steak** and **1902 / Hot Lion Chops**.
Gathering professions also return entries such as Skinning/Herbalism, so a recipe
entry does not always describe a crafted item. Recipe IDs are not item/spell IDs.
Only IDs/names are needed for the new guild crafter lookup; do not follow the
recipe links or fetch a catalogue merely to collect what the character knows.
See [recipe collection and query semantics](guild-roster-runtime.md#recipe-lookup).

Other focused requests returned 200 for sample equipment (16 equipped entries),
specializations, achievements, ruleset index (four), Normal ruleset, and its
auction-house index (15). These are research observations, not implemented
features. In particular the achievement array's **16 entries are not 16 earned
achievements**: `total_quantity` is 1. Only Novice Shaman (62006, account-wide,
reach level 25 on a Shaman, reward one Legacy Point) is marked completed; the
remaining entries contain incomplete spelunking/exploration progress. API
tracking does not establish the presence of a visible in-game achievements UI.

Static class/profession indexes still work (nine classes, 13 profession catalogue
entries); the plain alias still resolves to `static-1.60.1_70399-forever-eu`.
Realm and connected-realm indexes remain 404. The updated
[community notes](https://mhuijskens.com/WoWForever/Api/), dated 5 November, report
that public character profiles require level 10, while the guild roster includes
lower-level characters. We have not independently established that threshold.
The checks saved no credentials or raw response bodies and did not write Mongo
or send Discord messages. Full guild membership completeness, cache latency,
character coverage, and representative live Grub replies still need validation.

## Implementation decision after this checkpoint

On 4 November the administrator chose to prepare the **live** integration for merge
using expected API behavior, then debug real responses after launch. The confirmed
target is EU / Crossroads Mega Chippy (`crossroads-mega-chippy`). The shipped bot now
uses the Blizzard source by default, not the old file mock. Default assumptions are
the realm-free `/data/wow/guild/{guild}/roster` route, `name`/`surname` character
fields, and the returned character link or realm-free character-professions route.
See [runtime contract and failure behavior](guild-roster-runtime.md).

This is an implementation choice, not evidence that these routes have started
returning data. The dated observations below remain unchanged. Schema/HTTP failures
preserve previous data and cannot become an empty guild or fabricated milestones.

At **21:38:16 UTC on 4 November**, the updated authenticated probe checked the
configured realm-free `/data/wow/guild/crossroads-mega-chippy` and `/roster` paths;
both returned 404. The guild is the intended launch target, not a confirmed existing
published guild at this pre-launch point. The live source is prepared despite this
result, under the administrator's rollout decision; it does not interpret the 404
as empty membership or switch to fake data. Static/dynamic baseline results were
unchanged from the earlier check.

## Pre-launch recheck — 4 November 2026, 21:05–21:07 UTC

Reran the authenticated EU probe, then repeated it with `--character davina-asaria`,
the public example linked by the community account-profile research. OAuth returned
200 in both passes. This is a reported reserved character, not one of our own played
characters; its failure does not establish post-launch profile availability.

| Request | Previous observation | Current observation |
| --- | --- | --- |
| Invalid control namespace, realm index | 403 | 403 |
| `dynamic-forever-eu`, region index | 200; 1 region | 200; 1 region |
| Same namespace, realm index | 200; 26 entries | **404**, repeated in both passes |
| Same namespace, connected-realm index | 404 | 404 |
| `static-forever-eu`, class index | 403 | **200; 9 classes** |
| Versioned static class index | 200; 9 classes | 200; 9 classes |
| Versioned static profession index | 200; 13 catalogue entries | 200; 13 catalogue entries |
| `profile-forever-eu`, public example character | Not tested by us | 404 |
| Same character, `/professions` suffix | Not tested by us | 404 |

The newly working plain static alias reports
`battlenet-namespace: static-1.60.1_70399-forever-eu`. Future static-data integration
can use the alias rather than require a hard-coded build, subject to subsequent
verification. Successful region/static responses now carry `Last-Modified` values
of **4 November 18:47:35–36 UTC**, still with `public, max-age=86400`. This proves
republishing, not changed gameplay content or player-data freshness. The two class
responses have matching counts; the probe does not compare their full bodies.

[The updated community notes](https://mhuijskens.com/WoWForever/Api/), checked by their
author at 19:15 UTC today, also report the plain static alias opening during launch
maintenance, plus Korean region access since 3 November. We did not independently
probe Korea. Their realm list was still available at that checkpoint, unlike our
later EU result; the reason for the current 404 is unknown. Forever has no
player-facing realms, so this index is not a prerequisite for our roster integration.
Do not require Classic-style realm selection or replace cached membership with an
empty roster because an unrelated index fails.

The same notes still report public character/guild lookups returning 404. A
[2 November community forum update](https://us.forums.blizzard.com/en/blizzard/t/world-of-warcraft-forever-api-namespace/59774/3)
reports account lists/collections with a user token, but distinguishes an unrouted
realm-free character link from realm-based JSON 404s. Neither is a verified public
profile contract. No new official support announcement was found in this check.
We did not make a guild request because we still lack a known live guild target.

**Observed outcome:** genuine static-data namespace progress, but no verified guild
roster or character-profession responses. Retest after launch with a
known played/logged-out character and actual guild routing. The earlier evidence
below is retained as a dated baseline, not a claim that the plain static alias is
still forbidden or the realm index is currently populated.

## Evidence reviewed 2 November 2026

This is a historical research checkpoint, not a supported production contract. At
this checkpoint the roster feature still used an opt-in mock; the standalone
app-token probe remains separate from the worker.
Run instructions and credential setup are in the [README](../README.md#forever-api-probe).

Sources:

- [Blizzard API forum, 31 October community report](https://us.forums.blizzard.com/en/blizzard/t/when-will-we-gain-api-access-to-forever-apis/59595/10).
- [M. Huijskens' unofficial probe notes](https://mhuijskens.com/WoWForever/Api/), last
  checked by their author on 31 October at 22:45 UTC. This is not Blizzard documentation.
- [Earlier namespace discussion](https://us.forums.blizzard.com/en/blizzard/t/world-of-warcraft-forever-api-namespace/59774)
  contains no confirmed Blizzard namespace announcement.

The community notes report:

| Area | Reported result |
| --- | --- |
| `dynamic-forever-{region}` | Regions/realms opened 30 October, 23:46 GMT, on EU/US/TW |
| `static-1.60.1_70399-forever-{region}` | Static data works; plain `static-forever-{region}` gives 403 |
| `profile-forever-{region}` account profile | `/profile/user/wow` works with the player's user token and `wow.profile` |
| Public character profiles | Reported returned links still give 404 |
| Guild/profile roster routes | Tested realm-based paths still give 404; no proven complete roster |
| Connected realms/auctions | Still 404 in the reported tests |

The build-specific static namespace is a candidate, not a permanent default for a future
adapter. A client build number need not equal its API static build. Do not guess patch
names indefinitely or treat an outdated static namespace as evidence all APIs are down.

## Authenticated check from this branch

Ran the standalone probe against EU at **2026-11-01 23:08:11 UTC** with our own
client credentials (2 November local session date). OAuth returned 200. No response
bodies or credentials were saved.

| Request | Observed status/summary |
| --- | --- |
| Realm index, intentionally invalid namespace | 403 |
| Region index, `dynamic-forever-eu` | 200; 1 region |
| Realm index, `dynamic-forever-eu` | 200; 26 realm entries |
| Connected-realm index, `dynamic-forever-eu` | 404 |
| Class index, `static-forever-eu` | 403 |
| Class index, `static-1.60.1_70399-forever-eu` | 200; 9 classes |
| Profession index, same versioned static namespace | 200; 13 catalogue entries |

Successful responses advertised `Cache-Control: public, max-age=86400`; their
`Last-Modified` values were 30 October 23:46:50–51 UTC. Those are document/cache
timestamps, not measured player-data freshness. This pass confirms the dynamic and
versioned static data independently. **No guild or character target was supplied**;
their availability, name mapping, and profession ranks remain unverified by us.

## Names, identity, and realm assumptions

The reported **account** character entries use `name` (first name), `surname`, numeric
`id`, `level`, and `playable_class`. They link to
`/profile/wow/character/{firstname}-{surname}` without a realm segment. This does not
prove that a future guild roster will expose the same shape. Our mock's
`first_name`/`last_name` fields remain fictional; a live adapter must explicitly map
verified fields into the existing first/last-name domain model. Do not rename mock
fixtures merely to imply that they are captured API responses.

Players' ruleset selection does not mean the API has no internal realms. The notes
show internal CAMWOW realm IDs/slugs; protected-character links also contain realm
IDs. Keep the shared roster model realm-optional, and allow an adapter to retain any
verified routing/identity scope it actually needs. Follow verified returned links
after validating their host/path; do not require a guessed player-facing realm or
assume Classic realm/name routing. Public character URLs, guild routing, and stable-ID
scope all still need confirmation.

An account's own characters cannot replace guild membership. Adding Battle.net login
solely to collect one member's characters will not unblock a complete guild leaderboard.
Static profession catalogues also do not prove character profession/rank coverage.

## Probe boundary and interpretation

The standalone probe obtains an app token from `https://oauth.battle.net/token` using
client credentials. It sends bearer tokens only to a fixed regional Blizzard HTTPS
host; redirects are disabled and response links are not followed. Credential input is
local user secrets or environment variables, never flags. It does not access the bot's
secret store. Response bodies are bounded to 2 MiB and never printed or saved. Only
fixed JSON field names/counts and selected diagnostic headers are shown, with credential
redaction and escaped header text. No request/exception dumps, automatic retries,
background polling, or user-token/account-profile flow are implemented.

The default pass checks an intentionally invalid namespace as a control, dynamic
region/realm/connected-realm indexes, and unversioned/versioned static class indexes
plus a static profession index. Optional known character and guild targets check the
reported character route and realm-free guild routes; `--realm` explicitly selects
the older route form for diagnostics only. Those are explicitly
diagnostic candidates, not an implemented roster adapter. A guessed route returning
404 proves neither that the namespace is open nor that the feature is unsupported.
Even an existing namespace can contain an unpublished/absent resource. A 403 alone
does not distinguish an unknown namespace from a restricted one. The probe reports
statuses without promoting either into an empty roster or a support claim.

Store only reviewed, sanitized findings in this page. Any successful guild response
still needs a separately captured sanitized fixture and completeness/pagination checks
before it can replace membership. The probe does not capture such fixtures automatically.

## Remaining launch checks

Testing our own played characters/guild is blocked until the 4 November launch;
no suitable live target is available to us at the 21:07 UTC pre-launch check.
The public reserved-character example above is not a substitute. Resume the
guild/character checks after launch rather than interpreting arbitrary-target
404s as evidence of support.

- Confirm the current namespaces using authenticated requests and eventual official docs.
- Test a known real guild and character; verify routing, stable IDs, two-part names,
  membership completeness/pagination, and levels. An arbitrary-name 404 is inconclusive.
- Verify character professions and primary/secondary/tier/rank coverage separately;
  level collection can ship without profession collection.
- Measure publication after controlled in-game changes/logout, plus cache headers,
  conditional requests, throttling, and failure behavior.
- Correct the implemented `IGuildRosterSource` against these findings without
  changing its live-source identity silently or reviving a runtime mock fallback.
  The addon fallback remains paused.
