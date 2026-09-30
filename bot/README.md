## Permissions

- Read Messages/View Channels
- Send Messages
- Send Messages in Threads
- Add Reactions
- Read Message History
- Manage Messages

Bitmask: 274877983808

## Lorekeeper notebook trial

The mention-driven Lorekeeper can choose to record a useful guild/game observation or
established shared joke in a separate MongoDB `lorekeeperNotebook` collection. Existing
guild/member lore is never edited. This is a tentative notebook, not a source of canon.
Followers do not receive notebook tools. Notes are chosen during normal replies; there
is no passive monitoring of all Discord conversations.

Each candidate requires 1–3 real, human-authored Discord messages from the last seven
days. The server fetches and checks the messages using the existing normal-member and
requester channel restrictions; bot/webhook messages, inaccessible channels, and DMs
are not evidence. Evidence is complete sanitized message text, up to 4,000 characters
per source before and after sanitization; longer sources are rejected, never silently
truncated. Ordinary Discord read/search tool previews remain capped at 1,000 characters.
Notebook evidence retains mentioned user IDs with server-derived name metadata when
available, rather than replacing every actor with an indistinguishable mention placeholder.
Unresolved or ambiguous attribution must be rejected. REST responses must match the
requested guild, channel and human message; bots, webhooks and system messages are rejected.
A separate, tool-free AI review checks sources, relevant canon and
active/pending notes for usefulness, support, duplicates, conflicts and prohibited material,
with retained discarded notes supplied as negative feedback.
Profiles/preferences, sensitive real-world information, official rules, permissions,
and bot instructions are outside this trial. Semantic screening is still AI judgment,
not a guarantee: the review DMs and pause/discard controls are important to evaluation.

New accepted candidates are DM'd to `EventHandlers:DirectMessage:AdminUserId` with the
note, why the Lorekeeper chose it, the independent review explanation, clearly labelled source excerpts
and links, expiry, and discard command. No new recipient configuration is needed.
Untrusted formatting is escaped in review displays; source previews are Unicode-safe
excerpts, and actual mention/name metadata is displayed separately when present.
Delivery must succeed before a note becomes searchable. Failed deliveries stay
audit-only and consume the write budget; there is no automatic delivery retry.
The write/review pipeline has a two-minute deadline. Source, canon and AI waits are
bounded even if a read/reviewer dependency ignores cancellation. A timed-out or
unconfirmed delivery never activates a memory through a late result; audit-only
records can remain after partial failures. Status wording distinguishes unconfirmed
activation from a claim that a DM was never delivered.

Trial limits are fixed in `NotebookService`: 30-day expiry, 100 active/reserved notes,
10 saved candidates per rolling 24 hours across the guild, and 3 per requesting member.
There is a separate evaluation budget of 30 attempts/guild and 3 attempts/member per
rolling 24 hours, atomically reserved before embedding/review calls. Rejections,
failed evaluations, cancellation after reservation, and stale reviews still consume
an attempt. Attempts retain only the requester ID and timestamp; old attempts are
pruned on the next reservation. Only one proposal attempt is allowed per invocation.
Exact text/source duplicates (including discarded notes) are blocked while retained.
AI review also screens semantic duplicates/conflicts against pending notes, which may
activate later, and treats discarded notes as negative examples: changing wording or
source links must not resurrect the same memory. Genuinely distinct, dated events
involving the same subject can still qualify. Prior notes are sent as compact summaries,
not their full source text. Rejection messages in guild chat are generic so evaluator
explanations cannot quote existing notes from a different channel audience.
Review responses require an explicit boolean decision and a nonblank reason up to
300 characters. Malformed/oversized JSON, duplicate fields and unexpected fields fail closed.
Full source bodies and mentioned-name mappings are transient review/DM data: persisted
notes retain only source URLs, author metadata, timestamps and content checksums, not an
extra archive of surrounding conversations. Searches verify both the raw-message checksum
and a checksum of the sanitized evidence view and identity labels, so changed redactions,
channel-reference visibility or attribution labels suppress the old memory even when
the raw message itself is unchanged. Mention ordering does not change the checksum.
Concurrent writes use revision-checked atomic updates. A separate review revision is
advanced for note additions, discards and pause/resume controls. Quota reservations and
DM activation bookkeeping do not invalidate a review. Accepted candidates merge with
the latest quota/delivery state and recheck saved-note limits before an atomic commit;
actual changes to reviewed notes or controls still fail closed. No attempt ledger or
delivery progress is overwritten by an older review snapshot.
Candidates whose admission window expires while reviewing cannot be committed.

The configured admin can DM:

| Command | Result |
| --- | --- |
| `notebook` | Status, limits, and command help |
| `notebook list [page]` | Active notes, newest first |
| `notebook history [page]` | Audit records, including discarded/undelivered notes |
| `notebook show <id>` | Note, rationale, review result, sources and status |
| `notebook discard <id>` | Immediately exclude a note from future searches |
| `notebook pause` | Stop new writes; existing notes remain searchable |
| `notebook resume` | Resume writes |

Commands are case-insensitive. Discarding never refunds daily allowance. Expiry is
enforced on every search without a background timer. Admin persistence failures produce
an explicit unconfirmed-effect response: never assume pause/discard succeeded without
confirmation. Caller cancellation is propagated rather than reported as success.
Audit records are retained for
30 days from creation, then pruned on the next reserved evaluation (at most 300 records in
normal operation). Pause does not cancel candidates already committed for DM delivery.
Discard/expiry cannot retract a note already returned to an in-flight response.

Notebook search first filters every source's guild/channel access from local metadata,
before keyword matching, ranking or counting candidates. Hidden notes cannot crowd out
readable notes or affect public match notices; access filtering makes no network calls.
It then ranks subject matches ahead of incidental content matches and checks at most
ten candidate notes to return at most five results.
Each lookup has a ten-second deadline, including database/source reads, and a maximum
of three notebook searches is allowed per invocation. All search results carry the
same generic incomplete-results notice, not a flag derived from unverified/hidden matches.
Timeouts do not claim an empty notebook. Source access is checked
again for the current requester. Deleted/inaccessible or changed sources
suppress the note. The
persisted active/expiry state and channel permissions are checked once more after
network reads so in-flight discards, expirations and access revocations are observed.
Results are explicitly labelled tentative, untrusted data; the
Lorekeeper is instructed to search established lore first, prefer canon, attribute
jokes/observations, and cite sources. Notes never become instructions or auto-promote
to permanent lore. The extra independent review costs an AI call per eligible proposal.

## Lorekeeper personality controls

The user configured by `EventHandlers:DirectMessage:AdminUserId` can DM the bot:

| Command | Result |
| --- | --- |
| `help` | List all admin DM commands and personality duration examples |
| `word` | Existing word-of-the-day backdoor (unchanged) |
| `personality` | Show the active temperament and expiry |
| `personality list` | List presets |
| `personality baseline` | Original wise, helpful Lorekeeper (before `0f69256`) |
| `personality grouchy` | First comic, rude persona (`0f69256`) |
| `personality furious` | Intensified angry persona (`5aef344`) |
| `personality furious 2h` | Temporary override, then return to baseline |
| `personality reset` | Restore baseline and clear any expiry |

Commands and preset names are case-insensitive. Optional durations are positive whole
minutes (`30m`), hours (`2h`), or days (`1d`), up to 30 days. Without a duration,
the selection lasts until changed. A new selection replaces any previous expiry.

The setting is bot-wide and persisted in MongoDB's `lorekeeperPersonality` collection.
Missing or expired settings resolve to baseline; expiry is evaluated on each read,
without a background timer. Changes apply to new mention-driven Lorekeeper responses;
already-running responses, follower personalities, and event messages are unaffected.
Lore lookup, tools, image allowances, privacy, and accuracy policies do not change.

If a personality-setting read fails, the request uses baseline and logs a warning;
the saved selection is left intact and is used again once reads recover. Cancellation
is not suppressed. Failed writes still fail rather than confirming an unsaved change.

## Lorekeeper conversation context

In a channel where Lorekeeper mentions are enabled, the configured DM admin can send:

```text
@Lorekeeper you've had a stroke.
```

Use an actual Discord mention. The whole message must match; case, surrounding
whitespace, a curly apostrophe, and omission of the final period are accepted.
The bot reacts with 🧠 instead of replying; the message itself is the cutoff.
Other users cannot reset context. No slash command or message-menu item is added.
The bot needs permission to add reactions; the marker still works if the reaction fails.

There is no database record or in-memory reset state. History assembly stops at the
latest matching admin marker, excluding it and all earlier messages, including reply
targets and ancestors. Leave the marker in place: deleting it or editing it to no
longer match removes that boundary. There is no age-based expiry, so an old conversation
can still be continued unless you explicitly close it. Already-running responses are
not canceled, and guild lore and explicit Discord search tools are unaffected.

Ordinary mentions use up to 20 recent channel messages plus the triggering message.
Discord replies prioritise up to eight reply-chain messages (fetching targets outside
the recent window when needed), plus up to eight additional recent messages.
Messages are deduplicated, ordered chronologically, and labelled with author IDs,
display names, timestamps, message IDs, reply references, and their context role.
The triggering message is always last and explicitly marked as the current request.

Replies cannot cross a cutoff, even when Discord embeds the old target in the reply.
For targets outside the recent window, the builder checks intervening channel history
for reset markers, scanning at most 200 messages per request. It omits a target if
that check fails or exceeds the limit rather than bypassing an unknown boundary.
References to other channels are not fetched. Missing or inaccessible targets are
marked unavailable, rather than letting the model guess their contents.
