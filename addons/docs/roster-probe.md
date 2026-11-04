# Grub Roster Probe

## Purpose and status

**Paused:** the roster feature now plans around an assumed release-time Blizzard
web API. Keep this addon as a fallback experiment; in-game probe testing is no
longer a prerequisite for the API path. API availability is still unconfirmed.

The paused probe still records realm-shaped diagnostic fields from the APIs it
was built to inspect. Those are not a production identity model: Forever has no
realms and uses Firstname Lastname character names. Revisit this collector's name
and identity handling if addon work resumes; do not reuse its schema unchanged.

This standalone addon is an experimental slice of the
[guild roster plan](../../bot/docs/guild-roster.md). Its job is to establish what
Forever actually exposes before designing a production addon import path.
It is not a production import format and must not be used to replace a guild
membership list.

Local mock tests exist. **The addon has not yet been verified in the Forever
client.** A successful mock test, an existing function name, or an update event
does not prove complete or fresh game data. The production export contract stays
undecided until we have actual observations.

## What a capture contains

Each on-demand capture produces JSON with `format: "grub-roster-probe"` and
`schemaVersion: 1`:

- Client version/build/interface, locale, numeric region/project ID, realm,
  current player name/realm, and guild name/membership flag.
- Function availability, supported event registrations, event counts/timestamps,
  and the last ten manual refresh attempts in this addon session.
- `roster`: reported count, current offline-view flag when available, and the
  visible rows' names, classes, levels, and online flags.
- `guildProfessions`: the trade-skill API's visible headers and member rows,
  including skill-line IDs, header counts/collapse state, character names, skill
  ranks, and online flags where returned. Indexes preserve the source view;
  the probe does not guess a missing member-to-profession association.
- `playerProfessions`: the logged-in character's primary and secondary profession
  slots, skill-line IDs, names, ranks, and maximum ranks where returned. These
  self-only values are kept separate; they do not prove guild-wide visibility.
- Bounded diagnostic issues with API/field names and generic reason codes.
  Missing APIs, failed calls, restricted values, invalid types, and row limits
  remain visible without exporting arbitrary error text.

All coverage is explicitly `unverified`, even when reported and read counts agree.
`cacheFreshness` is always `unknown`. `capturedAt` is when the addon read the
client cache, not when the server last updated every character or skill.
Absent values are omitted, not replaced with zero, false, empty professions, or
invented maximum ranks. An available API returning zero rows is different from
an unavailable API. Header counts can include hidden/offline members.

Calls are guarded and API results are allowlisted. The collector bounds each
guild section to 5,000 rows and issues to 50; `issueCount` includes omitted issues.
An oversized section has `partial` status. This is a diagnostic limit, not a
statement about the game's guild-size limit.

## Refresh and privacy

The addon is idle until requested, apart from counting supported update events.
Opening it or clicking **Capture** only reads current data. **Request refresh**
calls `C_GuildInfo.GuildRoster`, with `GuildRoster` as a fallback when the modern
function is absent. Requests are limited to one per ten seconds; a successful
call is labelled `requested`, never `updated`. No update event may arrive, and an
event may have another cause. There is no automatic retry or server polling.

The addon does not change guild filters, expand/collapse profession headers,
load Blizzard UI modules, query recipes, send chat/addon messages, or make HTTP
requests. Refresh clears the displayed report; capture again after waiting.
Captured text stays fixed while events arrive, so copying cannot change it under
the player. If the client text box truncates a report, the addon clears it instead
of presenting a partial export.

There are no saved variables: reports and counters disappear on `/reload`, logout,
or restart unless you copy them. No public/officer notes, guild ranks, locations,
chat, GUIDs, or Battle.net identifiers are exported. The report **does contain
character, guild, and realm names**. Share privately; replace names consistently
before committing fixtures or posting publicly. Do not commit real guild exports.

## In-game test checklist

Copy each report to a separate local text file. Record the conditions alongside
it; keep exports out of the repository. Do not only provide the final report:
differences between conditions are the evidence we need.

1. **Load and copy:** confirm the addon appears in the Forever client, runs without
   Lua errors, and opens via `/grubprobe`. Check the recorded build/interface.
   Select/copy the full report, parse it as JSON, and verify the window scrolls,
   moves, closes with Escape, and reopens. Check non-ASCII character/realm names.
2. **Cold cache versus refresh:** capture immediately after login, request a
   refresh, wait several seconds, and capture again. Compare API availability,
   row counts, and update-event counters. If no event arrives, record that rather
   than assuming the cached data was refreshed. Repeated captures do not request
   server data; repeated refresh clicks within ten seconds should be refused.
3. **Offline members:** compare with the guild's known roster, including at least
   one known offline character. If the guild UI provides a show-offline control,
   capture with it off and on. Compare reported/read counts and specific names;
   changing the UI is manual and its previous setting should be restored after
   the test. Do not infer full membership from an online-only list.
4. **Guild professions:** capture before and after opening the guild professions
   view, if the client provides one. Expand profession headers manually and
   capture again. If no such view exists, record that limitation. Check a known
   online and offline practitioner, a known gathering profession, and a known
   secondary profession. Compare the returned ranks with what their owners see.
   Note collapsed/filtered headers and cases where names/ranks are absent.
5. **Own professions:** compare `playerProfessions` with your profession book,
   including missing primary slots and Cooking/Fishing/First Aid if learned.
   Self-only success is not evidence that one exporter can collect everyone.
6. **Changes and cache age:** if convenient, have a volunteer level a character
   or raise a profession rank. Capture before and after refresh, including an
   offline transition. Record whether changes require opening a UI, the member
   logging in, or more time. A test volunteer is preferable to inferring an
   update from changing counts alone.
7. **Permissions and empty state:** if available, repeat as a normal member rather
   than an officer, and on a guildless character. Expect unavailable/not-in-guild
   states without Lua errors. Do not change real guild permissions for this test.
8. **Report back:** provide the build, test conditions, expected versus observed
   values, and privately shared or consistently anonymized reports. Especially
   call out missing APIs, issues, offline gaps, and profession mismatches.

## Gate if addon work resumes

Do not advance a production addon exporter merely because the probe runs.
If that path resumes, first decide from the in-game results:

- Can one ordinary guild member reliably collect complete membership and levels?
- Which profession fields are guild-wide versus self-only, with what offline,
  filter, refresh, and secondary-profession limitations?
- Can we detect partial exports and preserve honest observation timestamps?
- Does an exporter need a UI action, a different API, or a narrower initial scope?

Then capture sanitized fixtures, document the observed constraints, and choose
the stable production addon export contract. If collection is inadequate, revise
the fallback plan before implementing its importer. Neither this probe nor its
JSON schema commits us to a particular production adapter or to a future Blizzard
web API.

## API references used for the probe

- [Forever UI source at the inspected revision](https://github.com/Ketho/wow-ui-source-forever/tree/bd2470aed543f72697a044e989285b6c83e63f73):
  `GuildInfoDocumentation.lua` documents `C_GuildInfo.GuildRoster` and update events;
  `Blizzard_ProfessionsBook/Camelot/Blizzard_ProfessionsBook.lua` uses
  `GetProfessions`/`GetProfessionInfo` for the logged-in character.
- [GetGuildRosterInfo reference](https://warcraft.wiki.gg/wiki/API:GetGuildRosterInfo).
- [GetGuildTradeSkillInfo reference](https://warcraft.wiki.gg/wiki/API:GetGuildTradeSkillInfo).

These identify functions and expected tuple positions, not verified behaviour
in the player's installed client. The capability map and live tests take priority.
