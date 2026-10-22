# Guild roster, professions, and leaderboards — draft plan

Status: the shared roster feature, leaderboard command, Grub tools, and milestone
delivery are implemented behind an opt-in, file-backed mock. There is no live
Forever API adapter or portal import. See [implemented behaviour and mock API
assumptions](guild-roster-runtime.md) and [local configuration](../README.md#mock-guild-roster).
A standalone diagnostic addon exists in
[`addons/GrubRosterProbe`](../../addons/GrubRosterProbe), with local mock tests;
it has not yet been verified in the Forever client and is now on the backburner.
See the [in-game probe checklist](../../addons/docs/roster-probe.md).
This records the requested direction and proposed first-version defaults,
not a claim that every detail below has been approved.

Review decisions: keep this small. Use database-backed pending/sent notifications,
not transactional crash recovery or exactly-once delivery. Secondary professions
can announce milestones. Do not build catch-up throttling/expiry controls unless
spam becomes an observed problem. API-terms-specific expiry/compliance feature
work is outside the initial implementation scope. The notification channel is TBD.

**Current direction:** assume an external Blizzard API will be available at
release for planning purposes, without treating that as a confirmed announcement.
Keep the source-independent model, but prioritize an API adapter rather than an
addon exporter. Addon testing is paused, not a prerequisite for API-path work.
Actual API endpoints, fields, completeness, and freshness still need verification
before shipping an integration. The diagnostic report is not a production contract.

## Direction

- Show a WoW: Forever guild character level leaderboard through a slash command.
- Include character professions and skill ranks where the source exposes them,
  supporting profession-specific rankings and guild crafter lookup.
- Make the same roster queries available to Grub through read-only tools.
- Announce selected level and profession milestones after observing them in
  accepted roster data, without turning every poll into a Discord post.
- Keep consumers independent of collection method: plan for an external Blizzard
  API adapter first. Retain the addon/paste approach as a paused fallback.
- External API support at release is a working assumption, not confirmed support
  or a promise that levels and professions will have the same endpoint coverage.

## First version

`/leaderboard` posts the top 10 guild characters by level in the invoking guild
channel. Include the character's full name (Firstname Lastname), class, and level.
Alts count as separate characters; this is not a ranking of Discord members or mains.
Defer the interaction before querying, following the existing command pattern.

Equal levels share a rank (1, 1, 3); full names give deterministic ordering
within ties, not evidence of who reached a level first. Bound each page to 10
rows and allow an optional page number; ranks apply across the entire snapshot.
Show the roster observation timestamp once. No data, unavailable storage, and an
out-of-range page have distinct responses; none should look like an empty guild.
Do not claim the list is live or infer levelling speed from the current snapshot.

### Professions

Include profession names and current skill ranks, with a maximum rank when known.
Support primary and secondary professions in the model, but only promise fields
verified in Forever. Do not require two primary professions or assume every
secondary profession is available through guild-wide collection.

Proposed `/leaderboard profession:alchemy` ranks characters by their known Alchemy
skill; omitting `profession` keeps the character-level leaderboard. Use the same
pagination and shared-rank rules. Compare only the same profession/skill-line tier,
not unrelated professions or tiers. Missing skill values are excluded from ranked
rows, not treated as zero. Show incomplete profession coverage once when relevant;
no known practitioners is not proof that the guild has none.

Grub should answer questions such as "Who has Alchemy?" and "Who has Blacksmithing
at 200 or above?" from structured profession data. A profession or skill rank does
not prove knowledge of a recipe, specialisation, availability, or willingness to
craft. Recipe catalogues and "Who can craft this item?" are deferred.

The [GetGuildTradeSkillInfo reference](https://warcraft.wiki.gg/wiki/API:GetGuildTradeSkillInfo)
lists profession IDs, member names, and skill ranks, and marks the function as
available in Forever. That is a collection lead, not proof of complete/current
guild data. The client spike must test offline visibility, refresh behaviour,
secondary profession coverage, and whether maximum ranks are exposed if addon
work resumes. This investigation is paused; the API path instead needs to verify
its own profession endpoints and fields. If one exporter cannot see other
members' professions, per-character addon exports or
addon-to-addon sharing are possible later fallbacks, not assumed first-version
capabilities. Level collection can ship without profession collection.

Proposed Grub tools:

- `get_guild_leaderboard`: the same ranked, bounded pages as the command, with an
  optional profession filter; defaults to character level.
- `find_guild_characters`: bounded name lookup returning full name, level, class,
  known professions, observation timestamps, and explicit ambiguity/pagination.
- `find_guild_profession_members`: bounded profession lookup with an optional
  minimum skill, returning matching characters, ranks, timestamps, and coverage.

Tools use the current Discord guild's configured WoW roster, not a guild ID
chosen by the model. They are available only in normal guild-chat Grub service,
not DMs, spontaneous followers, or the barred-customer branch. Keep the existing
customer-service execution guards. There are no import or edit tools.

Current levels and professions come from roster tools, not lore, notebook entries,
chat claims, or model memory. Preserve lore lookup for identity/history questions,
but update tool guidance so numeric roster queries do not depend on a lore search. Names
are data, never instructions; missing records are not invented. A tool result
must distinguish no snapshot, no match, and lookup failure. Grub should state the
snapshot age once when reporting levels, without repeating internal caveats.

## Shared data boundary

```text
Planned API adapter ──┐
Paused paste adapter ─┴─> validated roster data ─> Mongo store
                                                   │
                                          shared roster queries
                                             ├─> /leaderboard
                                             ├─> Grub tools
                                             └─> milestone detection
```

Proposed normalized snapshot fields:

- Scope: Discord guild mapping plus game variant, API region where applicable,
  and WoW guild ID/name. Forever has no realms; do not require a realm field or
  invent a realm slug to fit another version's API routes.
- Provenance: source kind, export/schema version, observed-at and imported-at UTC
  timestamps, importing actor, and a snapshot ID/content hash for repeat imports.
- Characters: stable source character ID where available, first name, last name,
  full display name, stable class identifier, and integer level. Preserve the
  spelling/Unicode of both name parts. Prefer API-provided name parts; do not
  reuse `Name-Realm` parsing or guess a split for ambiguous full-name input.
  Keep IDs optional for adapters that cannot provide them.
- Optional profession observations per character: stable profession/skill-line ID,
  tier where applicable, display name, nullable current/maximum skill, observation
  time, and coverage status (unknown, partial, or complete). Collection time is not
  a guarantee that the game's cached profession values just changed.

Roster completeness and profession coverage are separate. Missing profession data
means unknown, not no profession or skill zero; only an explicitly complete empty
profession list can mean none within its declared scope (primary/secondary/all).
An unavailable optional section does not invalidate a complete membership roster.
Validate any supplied profession entries; do not silently discard malformed ones.
Keep their actual observation times, and never relabel cached/previous values as
fresh after another roster import. An adapter without profession support reports
unknown rather than silently merging data from the previous source.

Prefer game variant + API scope + stable source character ID as the identity key.
Names are display/search fields, not the identity when an ID exists. A different
ID reusing the same Firstname Lastname is a new character and gets a fresh silent
baseline, not the previous character's milestones. A rename with the same ID
keeps progression attached to that ID. Class is not part of identity.

For a name-only adapter, use normalized first name + last name within game/API
scope, retaining original display spelling. Validate normalization against actual
payloads. Do not silently carry progression across a known departure and later
ambiguous name reuse; rebaseline conservatively. Name-only data cannot reliably
distinguish all renames or deleted/recreated characters between observations.
Do not hard-code the level cap to 60.

Keep one active source and one accepted complete membership/level snapshot per
roster. Publish that snapshot atomically so readers never see a half-imported list.
If professions come from separate requests, store their observations separately
per character/skill track, with independent timestamps and update ordering. A
roster-only refresh must not clear known same-source profession data; a late
profession response must not overwrite levels, membership, or newer profession
data. Compose these observations for queries, retaining their individual ages.
If one response supplies both levels and professions, consume both without
introducing an unnecessary second request or refresh schedule.

Reject stale updates and repeat imports idempotently. Prevent concurrent import
confirmations from overwriting a newer revision; reuploading an old export must
not make it appear fresh. Switching sources is explicit, not a field-by-field
multi-source merge or silent fallback to older addon data.

Only a verified complete roster can replace the current membership list. Reject
partial/online-only exports for the initial version rather than treating absent
characters as guild departures. Snapshot replacement does not delete lore or
member links. Pasted addon data is a member-supplied observation, not
Blizzard-verified ownership or proof of a person's identity.

### Membership lifecycle

Use the guild roster as the source of current membership when the API provides
it. An ID absent from a newly accepted complete roster stops appearing in the
leaderboard and Grub's current-guild queries. Stop its profession polling and
discard its unsent milestone notifications. A failed/partial roster request or a
failed profile request must not be interpreted as a departure.

Roster absence can cover a departure or deletion without proving which occurred.
A confirmed deleted profile also stops being shown as a current character.
Neither case deletes unrelated lore or Discord member links. Keep progression
keyed by stable ID so a different character reusing a name does not inherit it.
After a known departure, baseline the character silently if it later rejoins,
rather than celebrating progress made outside the guild. Discard late profession
results for characters that are no longer current members.

## API freshness — proposed starting policy

Assuming Forever follows the existing WoW web-API model, polling can only detect
published data, not observe live game events. Visible delay is Blizzard's
publication delay plus up to one polling interval and request/processing time.
Publication can depend on logout and server-side intervals; there is no verified
Forever latency bound. Fast polling cannot fix stale upstream data.

- Start with one guild roster check every 60 seconds, subject to actual endpoint
  caching guidance, rate limits, and observed publication cadence. Use roster
  levels directly if available rather than fan-out per character unnecessarily.
- Only use a separate 5–15-minute profession schedule if professions require
  different requests and polling them less often saves meaningful calls, payload,
  or processing. Use bounded concurrency and a shared budget if so. If roster
  responses already contain professions, refresh those fields with the roster.
- Commands and Grub read the shared cache. A request may trigger a deduplicated
  refresh when due; concurrent users must not each trigger a full guild scan.
- Honour cache/conditional-request support where available, and back off on 429s
  and service errors. Preserve the last successful data without claiming a
  failed refresh succeeded. A failed character lookup is not a guild departure.
- Store last-check/last-success times separately from source document modification
  times. A successful poll or 304 does not prove the underlying character state
  is current. `Last-Modified`, when supplied, describes the API document, not the
  exact moment every character level or profession rank changed.

The 60-second roster and conditional 5–15-minute profession intervals are starting
proposals, not guarantees or measured Forever behaviour. Measure launch-day level,
roster, and profession
publication after controlled changes/logout, then tune polling. Show honest source
age/check status; do not advertise real-time levelling or first-to-cap timing.

References for existing WoW behaviour, not a Forever contract:
[Blizzard's historical logout/interval explanation](https://us.forums.blizzard.com/en/blizzard/t/character-profile-api-not-updating-since-830/2967/4),
[conditional requests and document timestamps](https://us.forums.blizzard.com/en/blizzard/t/world-of-warcraft-api-update-visions-of-nzoth/3461/15),
and [Raidbots' profile update guidance](https://support.raidbots.com/article/12-why-is-my-character-out-of-date).

## Milestone notifications

- Character levels: every ten levels (10, 20, 30, etc.) and the verified game
  level cap, even if it is not a multiple of ten. Keep thresholds configurable.
- Professions: configurable rank thresholds, initially 75, 150, 225, and 300
  where applicable to Forever's actual profession model. Treat these as numeric
  milestones, not proof of a trained tier, recipe, or specialisation. A reported
  maximum changing from 75 to 150 is not itself a skill-rank milestone. Secondary
  professions are included when their ranks are available.
- Post to one configured guild channel (destination TBD), with no automatic
  member/role mentions. Use full character names; do not infer Discord ownership.
  The normal roster/Grub read tools do not grant posting authority.
- Use short deterministic copy, for example `Borin Ironhide reached level 20!` or
  `Élodie Firebrand reached 150 Alchemy!`. Combine observations for readability,
  splitting at Discord's message limits without silently truncating notices.
  No LLM call is needed to detect milestones or write each announcement.

Compare successive accepted, known numeric observations for the same character
and, for professions, the same skill line/tier. Trigger when the previous value
was below a configured threshold and the new value reaches or exceeds it; exact
equality is not required. If several thresholds were crossed between observations,
announce only the highest newly crossed threshold per character/progression track.
Do not invent an achievement time or ordering: these are newly observed milestones,
not proof that the character just dinged or reached a cap before someone else.

The first successful observation seeds a baseline silently, both for the initial
guild import and for newly seen characters/professions. Unknown/missing values,
failed/unchanged/stale responses, and a newly joined high-level member do not
generate retrospective celebrations. Rebaseline on an explicit source switch;
do not turn adapter differences into milestones.

Persist high-water marks and notification records in the database. Numeric
regressions or temporarily missing professions must not reset a baseline and
announce an already-recorded milestone again. Give each notice a unique key for
guild scope + character identity + progression track + threshold, preventing
ordinary repeated polls from creating duplicate records.

Keep delivery simple: save a pending notice first, send it through one delivery
worker, then save its sent status, sent time, and Discord message ID when available.
Only send notices successfully saved as pending; leave known send failures pending
for retry. The existing in-memory `NotificationQueue` is not the persistent store
for these notices.

Do not add transactions spanning snapshots, high-water marks, and pending records,
crash-point replay, or automatic uncertain-send reconciliation to the first
version. A crash before recording a notice can miss a milestone; a send accepted
by Discord followed by a failed sent-status save can produce a duplicate on retry.
These rare gaps/duplicates are an accepted best-effort tradeoff, not an exactly-once
guarantee. Normal pending/sent state survives restarts.

No outage-specific catch-up throttles, backlog expiry, or cooldown policy is needed
unless spam is actually observed. Keep the highest-crossed-threshold behaviour
above, rather than replaying intermediate thresholds. Secondary professions are
approved; only the destination channel is still TBD. This requires small
progression/delivery state, not a full historical snapshot archive or race system.

## Copy/paste import — paused fallback

Use the existing portal admin authorization policy initially; officer/self-service
permissions are a separate decision. The addon presents a versioned export string
that an admin pastes into a dedicated roster import page. No desktop agent needed.

1. Parse and validate size, version, configured guild scope, timestamps, character
   keys, class/level values, profession IDs/ranks/coverage, duplicates, and
   complete-roster status. Never execute pasted Lua, scripts, or other embedded
   instructions.
2. Preview character count, observation time, and additions/removals/level changes
   against the current snapshot, including profession changes and unknown coverage.
   Highlight unexpected decreases or a large drop in roster size; do not quietly
   clamp values or accept malformed rows.
3. Confirm the exact validated snapshot against the previewed current revision.
   Failures leave the current snapshot untouched and preserve a useful retry path.

Collect only the fields needed for the roster. Exclude public/officer notes, chat,
and Battle.net identifiers. Do not log raw pasted exports. Keep current roster
data separate from permanent lore and the temporary notebook. Historical snapshot
retention and growth tracking are outside the first version.

## Delivery slices

- [x] Build a standalone collection probe with copyable diagnostics and local
  mock tests. No bot, portal, or database integration.
- [ ] Verify the assumed release API: namespace, guild membership/level fields,
  stable character IDs and Firstname/Lastname fields, profession coverage, whether
  profession queries are separate, authentication, limits/cache headers, and actual
  publication delay. Forever has no realms. Capture sanitized fixtures; do not
  invent an endpoint contract.
- [ ] Paused: verify Forever addon collection in-client, especially offline members,
  completeness, refresh timing, two-part names, and available identifiers. Verify
  profession names/ranks, primary/secondary coverage, and cached/unknown values
  separately. Capture sanitized fixtures and define the versioned export format.
- [x] Build the roster feature's normalized model, validator, snapshot store,
  ranking, and lookup services with fixture-based tests. Follow the existing
  feature-project/Mongo separation; keep Discord, portal, and AI dependencies out
  of roster domain logic. Cover optional profession data, unknown versus empty,
  nullable ranks, profession/tier queries, stable IDs versus reused names, guild
  departures/deletions/rejoins, and a no-realm character model. Add any new test
  projects to the solution.
- [x] Implement a mock response adapter and background refresh over shared queries,
  with separate source/checked times, deduplicated refreshes, and independent
  profession reads. Use Classic-style roster and provisional Retail-style
  profession fixtures; do not guess a live Forever route. Test ordering, retained
  profession ages, stale/failed responses, and membership changes.
- [ ] Replace the mock with a verified live API adapter, including authentication,
  conditional requests/cache headers, HTTP backoff and rate limits. Reassess
  whether separate profession requests and their lower frequency remain useful.
- [ ] Paused: adapt the tested probe into a production exporter and add the admin portal
  paste/preview/confirm flow. Test
  authorization, invalid and partial exports, stale/duplicate imports, concurrent
  confirmations, and atomic replacement. Exercise the flow with `npm run dev`.
- [x] Register `/leaderboard` as an opt-in guild command. Test acknowledgement, ranking,
  profession filters, ties, pagination, output limits/escaping, observation times,
  empty/failure states, and isolation between guild scopes.
- [x] Expose the read-only Grub tools over the same queries. Test tool exposure,
  access guards, ambiguity, pagination, freshness, and failure semantics. Check
  representative live replies for accuracy and concise status copy.
- [x] Add milestone detection and configured-channel delivery using mock source
  fields. Live source coverage remains unverified. Test skipped threshold crossings, silent first
  observations, missing/failed data, source switches, regressions, profession tier
  separation, secondary professions, unique notice keys, database pending/sent
  persistence, and send-before-sent-status ordering. Verify departure cancellation
  and Discord message splitting. Test complete message copy and disabled mentions;
  do not require transactional crash recovery or exactly-once send tests.

Run the relevant backend build/tests/format checks and frontend build/tests/lint
for implementation changes. Replace this draft's proposals with confirmed
behavior as each slice ships; do not mark planned work as implemented.

## Decisions to confirm before implementation

- Command name and defaults: `/leaderboard`, top 10, public response, all alts.
- Optional profession rankings and crafter lookup; exact collection coverage is
  gated on the available API, not assumed from the level roster. The client spike
  is needed only if the addon fallback resumes.
- Guild identity, API region where relevant, and API configuration (no realm);
  import permissions can wait while the addon path is paused (admin-only portal
  remains its proposed default).
- Proposed polling cadence: roster every 60 seconds; profession details every
  5–15 minutes only if separate requests make that worthwhile. Otherwise consume
  professions alongside the roster. Adjust to actual API behaviour and limits.
- Whether to expose an exact age only or also mark snapshots stale after a chosen
  threshold; never silently hide old data or label it current.
- Milestone destination channel: TBD. Threshold defaults remain ten-level steps
  plus level cap, and profession ranks 75/150/225/300 where applicable. Secondary
  professions are included; no pings. No catch-up spam controls unless needed.

Defer mains-only/player rankings, Discord ownership claims, XP-within-level,
historical gains, first-to-cap races, scheduled leaderboard posts, desktop
uploaders, and multi-source merging. Also defer recipes, crafting specialisations, per-member
profession submissions, and addon-to-addon sharing. If member links are added
later, use explicit structured assignments rather than inferring ownership from
lore names or Discord nicknames.
