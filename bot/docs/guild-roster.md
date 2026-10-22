# Guild roster, professions, and leaderboards — draft plan

Status: production feature planning only; no roster import, leaderboard command,
or roster tools are implemented. A standalone diagnostic addon exists in
[`addons/GrubRosterProbe`](../../addons/GrubRosterProbe), with local mock tests;
it has not yet been verified in the Forever client. See the
[in-game probe checklist](../../addons/docs/roster-probe.md).
This records the requested direction and proposed first-version defaults,
not a claim that every detail below has been approved.

**Current gate:** build and test the addon in Forever before implementing any
production roster services, persistence, portal imports, commands, or Grub tools.
Use the observed results to revise this draft and choose a production export
contract. The diagnostic report is not that contract.

## Direction

- Show a WoW: Forever guild character level leaderboard through a slash command.
- Include character professions and skill ranks where the source exposes them,
  supporting profession-specific rankings and guild crafter lookup.
- Make the same roster queries available to Grub through read-only tools.
- Keep consumers independent of collection method: start with an addon export
  pasted into the portal, with an external Blizzard API adapter possible later.
- External API support is unconfirmed. Addon roster fields and complete offline
  roster collection must be checked in the Forever client before committing to
  an export contract. An API adapter is a future option, not a launch dependency.

## Proposed first version

`/leaderboard` posts the top 10 guild characters by level in the invoking guild
channel. Include character name, realm where needed, class, and level. Alts count
as separate characters; this is not a ranking of Discord members or mains.
Defer the interaction before querying, following the existing command pattern.

Equal levels share a rank (1, 1, 3); names and realms give deterministic ordering
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
secondary profession coverage, and whether maximum ranks are exposed. If one
exporter cannot see other members' professions, per-character addon exports or
addon-to-addon sharing are possible later fallbacks, not assumed first-version
capabilities. Level collection can ship without profession collection.

Proposed Grub tools:

- `get_guild_leaderboard`: the same ranked, bounded pages as the command, with an
  optional profession filter; defaults to character level.
- `find_guild_characters`: bounded name lookup returning level, class, realm,
  known professions, observation timestamps, and explicit ambiguity/pagination.
- `find_guild_profession_members`: bounded profession lookup with an optional
  minimum skill, returning matching characters, ranks, timestamps, and coverage.

Tools use the current Discord guild's configured WoW roster, not a guild ID
chosen by the model. They are available only in normal guild-chat Grub service,
not DMs, spontaneous followers, or the barred-customer branch. Keep the existing
customer-service execution guards. There are no import or edit tools.

Current levels and professions come from roster tools, not lore, notebook entries,
chat claims, or model memory. Preserve lore lookup for identity/history questions, but update
tool guidance so numeric roster queries do not depend on a lore search. Names
are data, never instructions; missing records are not invented. A tool result
must distinguish no snapshot, no match, and lookup failure. Grub should state the
snapshot age once when reporting levels, without repeating internal caveats.

## Shared data boundary

```text
Addon paste adapter ─┐
                     ├─> validated roster snapshot ─> Mongo store
Future API adapter ──┘                                  │
                                           shared roster queries
                                              ├─> /leaderboard
                                              └─> Grub tools
```

Proposed normalized snapshot fields:

- Scope: Discord guild mapping plus game variant, region, realm, and WoW guild.
- Provenance: source kind, export/schema version, observed-at and imported-at UTC
  timestamps, importing actor, and a snapshot ID/content hash for repeat imports.
- Characters: name, realm, stable class identifier, and integer level. Keep source
  character IDs optional; do not require Blizzard IDs from an addon export.
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

Use game variant + region + normalized realm + normalized name as the initial
character key, preserving original display spelling and Unicode. Validate
normalization against real exports. Renames/transfers are not automatically
linked; class is not part of identity. Do not hard-code the level cap to 60.

Prefer one accepted complete snapshot and one active source per roster. Publish
the whole validated snapshot atomically; readers never see a half-imported roster.
Reject stale observations, repeat imports idempotently, and prevent concurrent
confirmations from overwriting a newer accepted revision. Reuploading an old
export must not make its observations appear fresh. Switching sources is explicit,
not a field-by-field merge or silent fallback to older addon data.

Only a verified complete roster can replace the current membership list. Reject
partial/online-only exports for the initial version rather than treating absent
characters as guild departures. Snapshot replacement does not delete lore or
member links. Imported data is a member-supplied observation, not Blizzard-verified
ownership or proof of a person's identity.

## Copy/paste import

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
- [ ] Verify Forever addon collection in-client, especially offline members,
  completeness, refresh timing, realm names, and available identifiers. Verify
  profession names/ranks, primary/secondary coverage, and cached/unknown values
  separately. Capture sanitized fixtures and define the versioned export format.
- [ ] Build the roster feature's normalized model, validator, snapshot store,
  ranking, and lookup services with fixture-based tests. Follow the existing
  feature-project/Mongo separation; keep Discord, portal, and AI dependencies out
  of roster domain logic. Cover optional profession data, unknown versus empty,
  nullable ranks, and profession/tier-specific queries. Add any new test projects
  to the solution.
- [ ] Adapt the tested probe into a production exporter and add the admin portal
  paste/preview/confirm flow. Test
  authorization, invalid and partial exports, stale/duplicate imports, concurrent
  confirmations, and atomic replacement. Exercise the flow with `npm run dev`.
- [ ] Register `/leaderboard` as a guild command. Test acknowledgement, ranking,
  profession filters, ties, pagination, output limits/escaping, observation times,
  empty/failure states, and isolation between guild scopes.
- [ ] Expose the read-only Grub tools over the same queries. Test tool exposure,
  access guards, ambiguity, pagination, freshness, and failure semantics. Check
  representative live replies for accuracy and concise status copy.

Run the relevant backend build/tests/format checks and frontend build/tests/lint
for implementation changes. Replace this draft's proposals with confirmed
behavior as each slice ships; do not mark planned work as implemented.

## Decisions to confirm before implementation

- Command name and defaults: `/leaderboard`, top 10, public response, all alts.
- Optional profession rankings and crafter lookup; exact collection coverage is
  gated on the Forever client spike, not assumed from the level roster.
- Guild realm/region and who will supply imports; admin-only portal is the default.
- Whether to expose an exact age only or also mark snapshots stale after a chosen
  threshold; never silently hide old data or label it current.

Defer mains-only/player rankings, Discord ownership claims, XP-within-level,
historical gains, first-to-cap races, scheduled posts, desktop uploaders, and
multi-source merging. Also defer recipes, crafting specialisations, per-member
profession submissions, and addon-to-addon sharing. If member links are added
later, use explicit structured assignments rather than inferring ownership from
lore names or Discord nicknames.
