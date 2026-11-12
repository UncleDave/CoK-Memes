# Specials of the week

## Private, on-demand menu trial

The specials board turns distinctive recent guild happenings into food at Crossroads
Mega Chippy. It is not a newspaper, chat recap or permanent lore. Guild-wide changes,
consequential announcements, competitions, shared activities and achievements should
outrank disposable chatter, even when they appear in fewer messages. Smaller memorable
exchanges remain eligible. Aim for three to five distinct dishes, but use fewer or
none rather than filler. Food and preparation are invented; real member
actions, participation, purchases and event outcomes are not.

Prices are ordinary cosmetic chip-shop prices, not jokes derived from source numbers.
The writer must choose exactly one of `2 silver`, `3 silver`, `4 silver`, `5 silver`,
`6 silver`, `8 silver` or `10 silver`. The request schema constrains this enum and local
validation rejects anything else, including gold-denominated or inflated prices. These
are fictional menu amounts, not a claim about game vendor prices or a player economy.

The writing target is a joke, not a food-shaped illustration of the event. Find the
real irony, contradiction, excess or reversal; use the name as setup and the description
as Grub's blunt payoff through a ridiculous serving arrangement, contradictory promise,
missing ingredient or condition of sale. Sauce, shapes and packaging are not humour by
themselves. A hypothetical technique example teaches a finished joke without supplying
guild evidence. Relevant established lore can make the callback recognisable, but a
reference is optional and must improve the joke rather than merely name an old story.

Only the configured `EventHandlers:DirectMessage:AdminUserId`, in a human DM, can use:

- `specials` — help.
- `specials draft` — read the preceding seven days and privately preview a menu.
- `specials show` — repeat the pending menu, selected moments, sources and diagnostics.
- `specials approve <token>` — publish exactly the previewed menu text.
- `specials discard` — clear the pending draft.

There is no schedule, public generation command, image generation or additional
persistence. The Gazette experiment is ended: its commands, newspaper/image generation,
reader buttons, issue numbering and Mongo registrations are removed. Existing Discord
posts and historical `gazette` / `gazettePublishedEditions` Mongo collections are not
deleted or migrated. Old reader buttons no longer have a handler. Specials do not use
that history. This initial trial does not prevent repeating events across separately
requested menus.

## Audience and history discovery

Specials have a destination and optional source-channel subset under `Specials`
(`DestinationChannelId`, `DestinationChannelName`, `SourceChannelIds`). The committed
Development and Production configurations retain the former Gazette destination IDs.
Move any externally supplied `Gazette` destination/source overrides to `Specials`;
old keys are no longer read. There are no new credentials. The destination and sources must satisfy the
existing normal-member/publication-audience checks, independent of the admin's own
permissions or guild-member cache presence. Exclude private/officer, NSFW, thread and
voice channels; require bot read/history access. A destination readable by everyone
cannot receive material from member-only channels. Source access is rechecked during
reads and verification. Mentions are neutralised and inaccessible channel references
redacted. Only complete eligible human text is used; bot/webhook/system messages,
attachments, blank text and messages over the existing 4,000-character evidence limit
are excluded. Image-only competition entries therefore cannot be interpreted.

Unlike Gazette sampling, weekly history is paginated backwards from the exact request
time, continuing until the seven-day lower boundary or channel exhaustion. Each page
requests up to 100 raw messages. The oldest **raw** message advances the exclusive
before cursor, even when the entire page contains bots or excluded messages. Exact
timestamps are checked locally. Read channels round-robin, not only the first twelve;
cap a request at 100 history reads and each channel at 20 pages. Non-advancing cursors,
access loss and read failures discard that channel's partial evidence. Budget-limited
channels retain their readable partial history, explicitly marked incomplete.

The private preview reports fully read/eligible, failed and limited channel counts,
history reads, usable human messages and excluded records. The excluded count covers
all in-window records rejected by the text-evidence rules, not only human messages.
No fixed budget can guarantee full weekly coverage: busy channels can exceed 2,000
raw messages, and the global cap can leave channels unread. Coverage warnings are
shown when reads or later discovery are incomplete; do not imply exhaustive coverage.

## Discovery, comparison and context

1. Group collected messages by channel and request-anchored 24-hour slice. Split into
   complete, chronological batches of at most 100 messages and 32,000 serialized
   characters, retaining the preceding two messages when they fit. Rotate through
   channel/day runs, then pack complete small runs together within the same 100-message /
   32,000-character request bounds **before** applying the 40-request discovery budget.
   A quiet channel/day therefore does not spend a whole model call by itself. Keep
   each run's chronological context together; mixed channel/day runs are not evidence
   of a shared event. At most 40 discovery batches are assessed. Records too
   large for a batch are skipped whole and counted, never truncated; the private
   preview reports the exact number of collected messages not assessed.
2. Each tool-free structured-output call proposes at most four sourced candidate
   moments after surveying every included run. Major announcements do not need to be noisy or inherently funny. No
   topic/keyword allowlist singles out a rename or banner competition.
3. A separate selection call compares the whole candidate set, grouping announcements,
   discussion, symptoms and developments belonging to the same shared episode across
   batches/channels. Choose one strong representative angle per episode, not several
   dishes for routine updates or inconveniences. A different participant, number or
   timestamp is not automatically a distinct incident. Unrelated events on a common
   topic remain separate, and adjacent unrelated claims must not be bundled together.
   First consider guild-wide decisions/changes and collective projects or activities;
   ordinary participation or minor progress needs a distinctive turn to qualify.
   Prefer fewer strong moments over filling slots. Seek variety when strong evidence
   supports it, not through invented importance or splitting one episode.
   It chooses at most five events with one to three evidence anchors each. Candidate
   inputs over 90,000 serialized characters are first reduced in groups of 20, then
   compared again; this is bounded hierarchical selection, not a complete event archive.
4. Retrieve context from the already-read weekly history around each selected anchor:
   up to three preceding/following messages in the same channel within 30 minutes.
   Preserve all anchors; add nearest complete neighbours up to 12 messages/event and
   a soft 15,000 serialized-character budget. This requires no extra history sampling.
   Missing context is not evidence of an outcome. Resolve server display names through
   REST before final writing, reusing the existing 64-identity resolution bound.
5. The final writer receives raw context, not just summaries. It can omit an unsupported
   event and must distinguish proposals/announcements from completed events or winners.
   Each dish is bound to one distinct selected event and one to three exact source URLs
    from that event's context. Its private factual event summary shares those citations.

## Established lore callbacks

After event selection and server-name resolution, a read-only lore reader enriches
each selected event. Discovery and selection receive no archival lore: old stories
cannot become candidates for this week's activity merely because retrieval found them.

- For each event, search existing semantic lore using its selected summary and up to
  three resolved anchor-author names, with a 1,000-character query limit and at most
  four search results. Use guild-lore entries from these results; do not infer member
  identity from a semantic hit, name or alias.
- Independently look up up to three distinct participant accounts per event through
  the existing exact Discord-account link, prioritising cited authors, then cited
  mentions, then context authors. Cache these reads only within the draft. Include
  only a returned member whose linked ID exactly matches the requested account.
  Supply name and biography, not pronouns, nationality, character, roles or aliases.
- Interleave guild and linked-member candidates and attach at most three complete
  entries/event. Retain at most twelve distinct entries and a combined 12,000-character
  serialized attachment budget (repeated attachments count again). Entries over 4,000
  content characters or the remaining serialized budget are omitted whole, never cut.
  Private diagnostics report supplied/used and bound-omission counts. A failed lookup
  stops the draft rather than silently pretending no lore exists.

Permanent lore is the existing administrator-maintained guild/member context, not raw
private chat, notebook observations, editor instructions or revision history. It has
no per-channel audience metadata: retrieval itself does not prove every stored detail
is suitable for publication. Sensitive-topic restrictions still apply, member linkage
is explicit, and the administrator must review the actual callback before approval.

The final writer receives each entry as separately keyed untrusted data alongside,
not inside, the event's recent source records. Lore supports its own established
history/jokes only. Keep old expansion settings and identities historical; it is not
proof that an old mishap recurred this week, or permission to invent current actions.
Current speaker names remain resolved server names, not guessed aliases.

Every dish returns `loreKeys`: zero to two exact keys from that event for lore actually
used, or `[]` if no callback improves the joke. Local validation rejects invented,
duplicate or cross-event references. Recent-event `sourceUrls` still cite only verified
chat; `eventSummary` describes this week's event rather than laundering old lore into
fresh evidence. Each private source review names its dish's callbacks, and the preview
includes the complete used entries for review;
unused entries and provenance are not appended to the public menu.

Refetch used entries by name before preview and again on approval. Changed/deleted
text, entry type, name or member-account link invalidates the draft. Only the used
name/content/link snapshot is retained transiently; no lore is created or updated.

## Model and response contracts

Final menu writing and its optional text repair explicitly use `gpt-6.1-sol` through
the existing OpenAI Responses client. Discovery, event selection and their repairs
stay on the shared `gpt-6-luna` default. Other bot replies and image generation are
unchanged. There is no silent fallback to Luna if Sol is unavailable; the draft fails
at writing. This uses the existing OpenAI key, with no new credentials or setting.
Sol may have different latency/cost; no claim of better humour is established by
choosing the model alone.

All calls use named, strict JSON schemas and no tools. Final request data is capped at
100,000 serialized characters; oversized context fails closed rather than silently
cutting evidence. Local validation enforces fields,
types, lengths, counts, exact citations and event binding; provider schema adaptation
does not replace these checks. The OpenAI adapter turns unsupported schema validation
keywords such as `maxLength` into descriptive notes, not enforced constraints. Text-field
descriptions therefore also explicitly instruct the model on single-line, nonblank and
numeric length contracts. Local checks distinguish overlong output, missing text and
line breaks; they never truncate an event summary.
For those three text failures only, make one bounded repair request for the failed
response, with the same source/schema contract and original input plus the rejected
draft and fixed-field numeric diagnostic as untrusted data. Instruct it to preserve
facts and uncertainty; mechanically require the same incident/dish count, citations
per position, menu event bindings and used lore keys. Validate the entire repaired response again.
A second rejection stops drafting; provider, incomplete-response,
JSON/shape, citation, event-binding and other failures are never retried. Earlier
successful batches are not restarted. Repair input shares the 100,000-character ceiling.
High reasoning effort uses provider-default output
limits. Cost scales with chat volume: up to 40 discovery batches plus selection/reduction
and one menu-writing stage, each allowing at most one additional text-repair request;
this is deliberately an on-demand admin trial.

Model input, candidate summaries, identities and URLs remain untrusted data. Evidence,
privacy and sensitive-topic constraints apply throughout. Dated shared guild context
is interpretation only, never proof of an event or a source of menu ingredients.

## Preview and publication

The private preview shows the exact public menu, then each dish's selected real moment
and clickable source links, any complete established lore used, then coverage and
approval actions. Only the dated
menu, dish names, descriptions and prices are posted publicly. No source appendix,
diagnostics, token, disclaimers or extra explanatory footer accompanies the menu.
The menu is escaped for Discord markdown and bounded to one 2,000-character message;
all delivery uses `AllowedMentions.None`. Text posting needs View Channel and Send
Messages, not Embed Links or Attach Files. Missing posting permissions do not block
private drafting.

There is one in-memory pending specials draft, serialised across command instances.
A new draft clears/replaces the previous approval; all preview messages must deliver
successfully before approval becomes active. A random token binds the exact menu and
destination for 30 minutes, or until restart/discard/replacement. `show` never extends
expiry or regenerates copy. Conversational approval cannot publish a menu.

Before preview and again on approval, refetch cited messages and compare author,
timestamp, raw-content hash and sanitised-view hash, including fresh server names and
current source audience, and verify the used permanent-lore snapshots. Changed/deleted
evidence or lore, access loss or destination changes
require a new draft. This does not mechanically establish prose accuracy or validate
uncited context; the administrator must review the selected moments and sources.

Approval is consumed before verification/send. Repeated/concurrent approval cannot
send the same pending menu twice. Check destination/posting permissions again directly
before sending. Discord permission changes cannot be atomic with the external send.
An error/cancellation after send initiation can have an ambiguous outcome; do not
retry that approval. Inspect the destination first. Draft state and raw chat are not
stored or logged. Diagnostics log only static stage (plus numeric discovery batch/total),
exception type, fixed validation category/field and numeric length/limit, never raw
exceptions, prompts, chat or generated prose. Private failures identify the discovery
batch and, for text validation, the field and length/limit or missing/single-line issue.
Earlier successful batches and a text-repair attempt can account for time spent before
a later rejection. There is no output truncation or publication of a rejected draft.

## Validation and live review

Regression tests cover backward pagination/cursors, all-bot pages, exact time boundaries,
round-robin channels beyond twelve, global/per-channel budgets, access loss, discarded
partial reads, whole-message discovery, Unicode/serialized bounds, context corrections,
packing many quiet channel/day runs without unassessed evidence, affordable price enums,
request/schema/data boundaries, citation/event binding, private preview gating, admin
DM routing, expiry/replacement/discard, exact publication, concurrent approval and
ambiguous sends. Lore tests cover linked-identity boundaries, omission of personal
profile fields, whole-entry/serialized budgets, untrusted input separation, optional
callbacks, per-event keys, private review and edits/deletions/rebindings before approval.
Model tests check request/repair routing and actual Responses transport serialization,
not just client metadata. No static prompt substring test establishes event ranking or humour.

For the first deployed trial, use `specials draft` if the guild rename and banner
competition are within the current seven-day window; otherwise assess the next major
guild events live and use an approved evidence fixture from the original week for a
separate model evaluation of those two examples. Check that discovery finds both independently among competing
mundane chatter, joins relevant developments, preserves competition status and produces
recognisable dishes. They are held-out live-review examples, not runtime search terms
or hand-fed candidates. Deterministic tests prove evidence reaches discovery, not that
the model will recognise its significance. Live Discord delivery, permissions, event
selection and menu quality still require an admin trial after deployment.

The November 11 live preview read 1,306 messages from all 35 eligible channels but used
40 sparse discovery batches and left 472 messages unassessed. Use that distribution
as a coverage regression, not a set of hardcoded story choices. Its dishes also
overweighted routine progress and several aspects of a launch; judge the next live
selection against the whole candidate set, with one coherent angle per shared episode.

The November 12 preview found more coherent incidents but rendered them as food
illustrations (charter signatures in ketchup, five fishcakes and a takeaway bag), with
little payoff. Review Sol's next draft with the same incident evidence, both with and
without relevant established callbacks: does each item actually land as a joke without
the private explanation, and is the lore used accurately rather than name-dropped?
These previews remain live evaluation material, not runtime facts or prompt examples.
