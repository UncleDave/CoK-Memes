# Guild roster implementation

## Status and API assumptions

The roster, query, command, Grub-tool, and milestone paths are implemented against
an opt-in file-backed mock. **There is no live Blizzard HTTP/OAuth adapter yet.**
No request is made to a guessed Forever endpoint. The addon stays paused.
See [configuration](../README.md#mock-guild-roster) to run it and the
[design plan](guild-roster.md) for accepted scope and deferred work.

The user's Classic API references informed the mock:

- [Classic overview](https://community.developer.battle.net/documentation/world-of-warcraft-classic).
- [Classic game-data APIs](https://community.developer.battle.net/documentation/world-of-warcraft-classic/game-data-apis).
- [Classic profile APIs](https://community.developer.battle.net/documentation/world-of-warcraft-classic/profile-apis):
  the guild roster is a profile resource at
  `/data/wow/guild/{realmSlug}/{nameSlug}/roster`, despite its `/data` prefix.
- [Classic namespaces](https://community.developer.battle.net/documentation/world-of-warcraft-classic/guides/namespaces):
  Era uses `profile-classic1x-{region}`, not the progression namespace
  `profile-classic-{region}`. Guild resources update periodically and character
  resources on logout. This describes Classic, not a Forever guarantee.
- The inspected Classic profile catalogue does **not** list character professions.
  [Retail profile APIs](https://community.developer.battle.net/documentation/world-of-warcraft/profile-apis)
  do list a separate character-professions summary. Its primary/secondary/tier
  shape is a provisional profession-response model, not confirmed Era support.

The mock roster uses `guild` and `members[].character` objects, numeric character
IDs, levels, and `playable_class.name`. Its `first_name`/`last_name` properties are
explicit Forever assumptions; Classic's single-name/realm routes are not carried
into our domain. Profession files use `character.id`, `primaries`, `secondaries`,
`profession`, optional `tiers[].tier`, `skill_points`, and `max_skill_points`.
They are hand-authored fictional fixtures, not captured authenticated responses
or an assertion of a published Forever schema. `_mock_complete` is a test-only
switch: setting it false simulates a rejected partial membership response.

## Boundaries and persistence

- `ChampionsOfKhazad.Bot.Roster` owns the source interface, normalization,
  validation, refresh service, queries, rendering, and progression comparison.
  It has no Discord, MongoDB, or AI dependencies.
- `ChampionsOfKhazad.Bot.Roster.Mongo` implements snapshot compare-and-swap and
  notification storage. `guildRosters` stores current members and high-water
  marks; `guildRosterNotices` stores pending/sent notices under unique `_id`s.
- The host runs one refresh/delivery worker, checking due work every ten seconds
  while Discord is ready. Queries read the database-backed cache; commands/tools
  do not start independent refreshes. Repeated refresh calls coalesce through
  the service gate and persisted last-check time.
- Mock state is scoped by Discord guild and source ID (`mock-classic-v1` plus a
  hash of the fixture's absolute file path). Changing the configured fixture path
  starts an isolated dataset; changing a known guild ID inside a fixture is rejected. A
  future source must use its own ID incorporating its configured game/API scope,
  so mock progress/notices cannot leak into live data. No cross-source merging.

The mock reads a roster response file every roster interval (default 60 seconds)
and a profession response file per current character when due (default 600
seconds). Both intervals can be shortened to ten seconds for private tests.
File modification time is the mock's observation timestamp. Independent files
retain independent ages; this is never presented as game-event time.

A future source can declare that it supplies professions with the roster, in
which case no additional profession requests are made. Separate profession
responses cannot overwrite membership/levels, and older profession observations
cannot roll back newer ones. Failed/missing profession files retain the last
known data with a failed-refresh flag; they are not evidence of no profession.
Known-empty primary and secondary arrays differ from a missing section.

Only a complete, valid membership response replaces members. A departed/deleted
character stops appearing in current queries, stops being polled for professions,
and loses unsent notices. Rejoins silently seed their current progression; reused
names with different stable IDs are distinct characters. Name-only observations
receive a persisted instance key for one continuous membership; a known departure
and reappearance gets a new instance. They cannot infer every deletion/rename
that happens entirely between checks.

## Commands and Grub

`/leaderboard [profession] [page]` is guild-registered only while the feature is
enabled. Disabling it removes its own slash registration, not unrelated commands.
It defers publicly before queued work. Results contain ten characters per page,
competition ranks (1, 1, 3), full names, class, level or profession rank, and the
observation age. Mock output is explicitly labelled. Large text is split at
Discord limits; names are escaped and allowed mentions are disabled.

Profession filters accept a name, skill-line ID, or `ID:tier` returned for an
ambiguous filter. Unknown ranks are excluded, not zero-filled. Name lookup uses
case-insensitive full-name substring matching and returns paginated matches.
An empty guild, no matches, invalid page, no initial data, first-refresh failure,
and lookup failure remain distinct outcomes. A failed later refresh retains the
last data with a visible failure status. Profession rankings disclose incomplete
coverage and use their profession observation ages, not a newer roster timestamp.

Normal guild-chat Grub service gets `get_guild_leaderboard`,
`find_guild_characters`, and `find_guild_profession_members`. They are bound to the
configured Discord guild, read-only, and use the same queries. DMs, other guilds,
spontaneous followers, and barred customers do not get these tools. Existing
customer-service function guards also apply after a third strike in a conversation.
Current stats use roster data, not lore or web guesses; names/tool results are
quoted data. Recipe knowledge and Discord ownership cannot be inferred.

## Milestone delivery

Every ten character levels, optional configured `LevelCap`, and profession ranks
75/150/225/300 are compared against persisted high-water marks. Secondary
professions participate. Crossing rather than exact equality triggers a notice;
only the highest newly crossed threshold per track is queued. A changed trained
maximum alone does not trigger. Initial/new-character/new-profession observations
seed silently. Temporary unknowns/regressions do not reset progress.

Announcements require a channel ID. Mock announcements additionally require
`AllowMockNotifications=true`. While announcements are off, progression is still
tracked but notices are not queued for later replay. The destination must be a
text channel in the configured guild. Notices use full character names, no pings,
and deterministic copy; mock notices are prefixed `[Mock]`.

The service saves pending notices before sending, then records the sent time and
Discord message ID. Multiple notices are packed into messages within Discord's
limits. Failed sends remain pending. Database keys prevent ordinary duplicate
queueing; one worker serializes refresh and delivery. There is intentionally no
distributed-worker lease, transaction spanning state and pending records, or
exactly-once guarantee. A failure between saving progression and queueing can
miss a notice; a failure after Discord accepted a send but before marking sent
can duplicate it on retry. These are accepted best-effort tradeoffs.

No catch-up cooldown/expiry controls or full progression archive are implemented.

## Validation and remaining live checks

The existing backend test projects exercise mock response parsing, ordering and
coverage, rankings, guild isolation, names/IDs, departures/rejoins, milestones,
pending/send/sent ordering, failure handling, command registration/acknowledgement,
Discord mention suppression, Mongo command/serialization contracts, and Grub
tool exposure/customer guards. They run without Discord, AI, or Mongo credentials.

The opt-in `RosterMongoIntegrationTests` checks real snapshot/notice persistence,
unique notice IDs, pending/sent state across store recreation, and scoped cleanup.
Set `COK_ROSTER_MONGO_TEST_CONNECTION` to a disposable loopback Mongo instance to
run it with the normal backend test command. It creates and drops only a uniquely
named test database. It is skipped by default.

Real MongoDB behaviour and live Discord delivery are not proved by the default
test doubles. Before a live trial, use a disposable development database
and private test guild/channel; verify restart persistence and the walkthrough
in the README. Do not enable mock notices in the real guild by accident.
The eventual API adapter still needs actual Forever routes/namespace, auth,
response validation/completeness, cache headers/conditional requests, HTTP retry
and rate-limit handling, and measured publication timing.
