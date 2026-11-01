# Forever web API research

## Evidence reviewed 2 November 2026

This is a research checkpoint, not a supported production contract. The roster feature
still uses its opt-in mock; the standalone app-token probe is separate from the worker.
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
reported character route and provisional realm-based guild routes. Those are explicitly
diagnostic candidates, not an implemented roster adapter. A guessed route returning
404 proves neither that the namespace is open nor that the feature is unsupported.
Even an existing namespace can contain an unpublished/absent resource. A 403 alone
does not distinguish an unknown namespace from a restricted one. The probe reports
statuses without promoting either into an empty roster or a support claim.

Store only reviewed, sanitized findings in this page. Any successful guild response
still needs a separately captured sanitized fixture and completeness/pagination checks
before it can replace membership. The probe does not capture such fixtures automatically.

## Remaining launch checks

Known-character testing is blocked until the 4 November launch; no suitable live
character is available to us before then. Resume the guild/character checks after
launch rather than interpreting fabricated-target 404s as evidence of support.

- Confirm the current namespaces using authenticated requests and eventual official docs.
- Test a known real guild and character; verify routing, stable IDs, two-part names,
  membership completeness/pagination, and levels. An arbitrary-name 404 is inconclusive.
- Verify character professions and primary/secondary/tier/rank coverage separately;
  level collection can ship without profession collection.
- Measure publication after controlled in-game changes/logout, plus cache headers,
  conditional requests, throttling, and failure behavior.
- Only then implement `IGuildRosterSource` with explicit live-source identity, leaving
  the existing mock dataset isolated and keeping the addon fallback paused.
