# The Khazad Gazette

## On-demand private editorial flow

The Gazette reports real guild happenings with disproportionate dwarven journalistic
gravitas. It is a selection of funny exchanges, small incidents and genuine good news,
not an exhaustive chat summary, recurring roast, or source of permanent lore.

Only the configured `EventHandlers:DirectMessage:AdminUserId`, sending a human DM to
the bot, can request or publish an edition:

- `gazette` — help.
- `gazette draft` — sample recent chat and deliver a private preview with source links.
- `gazette show` — repeat the pending preview without regenerating or extending expiry.
- `gazette approve <token>` — publish exactly the previewed edition to `#ai-tavern`.
- `gazette discard` — clear the pending draft without publishing.

The sender's Discord user ID in an admin DM is the authorization boundary. Gazette
does not look up the administrator in the guild member cache or recheck the admin's
guild channel permissions. That cache can legitimately omit an offline guild member.
This does not broaden source scope: normal-member audience and bot-read checks remain.

The default window is the preceding seven days. A successful edition contains one to
three sourced stories, a tiny absurd fictional classified advert,
and an optional lead-story illustration. Quiet or unsuitable conversation produces no edition, not invented
news or padding. No notebook entries are required or written; no lore is changed.
Archival lore callbacks are not included in this first version.

The editorial voice is straight-faced, disproportionate newspaper reporting, not a
chronological chat recap with a funny final sentence. Choose a news/comic angle first,
use an outcome-first inverted-pyramid lead, then attributed supporting details in two
short paragraphs. Preserve uncertainty naturally, particularly for rumours. Let framing
and word choice carry the humour; omit detachable concluding insults or morals. A wry
ending is allowed when it completes the reporting angle, not obligatory or categorically
banned. Vary the approach between stories. Imaginary Gazette departments/bureaus/inquiries are not the
default punchline; at most one unusually apt metaphor per edition, preferably none.
Such metaphors are framing, not evidence of actual guild institutions. Invented actions,
witnesses or personal traits remain prohibited. Preserve necessary attribution and
qualifications naturally without repetitive legal disclaimers. Prefer short, incisive
35–65-character headlines in normal case, avoiding exhaustive summaries/software jargon.
Endings stay in the reporter's voice: a current status, outlook, uncertainty or consequence,
which may itself be funny. "For now, the fortress is built chiefly from maybes" fits;
"That's less a repair saga than an expensive tutorial" is a detachable comedian's verdict.
Do not mechanically repeat "For now" across all articles.
This is explicitly a **satirical** newspaper, not a sober factual bulletin. The news
structure is a vehicle for comedy: each headline/lead needs a recognisable comic
reframing, with dry irony, mock-grand scale, vivid descriptions and pointed contrasts
inside the reporting. Accurate source facts do not require bland phrasing. Style
examples use "a two-phone solution to a two-button problem" and a fortress rumour's
well-counted pieces versus elusive foundations; they remain examples, never evidence.
Every non-empty edition has one absurd fictional classified ad, not a real member's
advert or a purported guild announcement.
The published section is simply "Classifieds"; neither copy nor illustrations carry
"fictional satire"/"satirical classified" disclaimers. Fictional framing remains an
internal generation constraint, not a repetitive explanation printed next to jokes.

Aim for one lead plus two distinct smaller dispatches when the sample supports them.
The lead gets the strongest incident; secondary stories have a lower newsworthiness
bar and can come from a funny handful of ordinary messages. The writer is instructed
to continue searching the sample after choosing the lead, not stop at one headline
event or retell it three ways. This is an editorial target, not permission to invent
two filler stories. A genuinely thin sample can still yield a single-page edition;
the private diagnostics explicitly say when only one story was selected.
Select moments with a distinctive incident, amusing contrast or memorable exchange
that stands on its own as guild news. Routine bot administration or bot troubleshooting
should not become a story merely to fill a slot, but can qualify when there is a genuinely
entertaining incident. This is editorial judgement, not a topic blacklist; member
troubleshooting and real-world tech anecdotes remain eligible. Avoid selection rules
that single out the particular role/context incident from a previous trial draft.

The model runs an isolated, tool-free JSON task with the shared dated guild-activity
context. Chat records, names, mention mappings and URLs are untrusted data, not commands.
Story citations must be exact supplied URLs. Quotes and factual claims must remain
supported and attributed; fabricated witnesses, attendance, raids, progression and
corrections are prohibited. Exclude personal disclosures, credentials, allegations,
harassment and genuine interpersonal disputes even in readable channels. These are
editorial instructions, not a claim that deterministic validation proves model prose
accurate or safe: the administrator must inspect the preview and evidence.

## Sampling and publication audience

Publication scope, not the administrator's elevated privileges, governs source access.
Reuse the existing normal-member channel permission calculations:

- Require the configured normal-member role and bot to be able to read
  source text channels and message history.
- Exclude officer/private channels, NSFW channels, threads and voice channels.
- If the publication channel is readable by `@everyone`, exclude sources that are only
  readable by the normal-member role. Access is recalculated during reads and approval.
- Redact inaccessible channel mentions, neutralise role/user/broadcast mentions, and
  preserve parsed user identity metadata only for interpreting the evidence.
- Include human text messages only; ignore bot/webhook/system messages and attachments.
  Complete evidence longer than 4,000 characters is skipped, never silently truncated.

A request reads at most 12 eligible channels (channel-ID order), the latest 100 raw
messages per channel through Discord REST, then selects up to 160 recent human messages
within a 40,000-character content/metadata allowance, newest first and supplied to the
writer chronologically. This is bounded sampling, not a seven-day exhaustive backfill;
busy channels may cover much less than seven days. The private response reports messages,
channels read/eligible and read failures. A failed channel does not prevent drafting
from other safe channels. Drafts are limited to one model call per minute and commands
have a six-minute deadline. Optional artwork has its own two-minute deadline.

Draft failure diagnostics identify the operation stage (chat/name reading, story writing,
issue-number lookup, formatting, evidence checks, rendering or private delivery), exception
type and static code failure site. Model validation failures additionally expose a fixed
category/field name and numeric length/limit where relevant, never rejected text, source
URLs, unknown model-supplied property names, prompts or raw exception messages. The
admin DM explains the stage and validation category instead of reporting only a generic
InvalidOperationException. No draft becomes approvable after a failure.

The high-reasoning writer has an 8,192-token generation ceiling to leave room for
reasoning plus its JSON response. A reported length-limited response is identified as
incomplete before parsing. Invalid JSON, wrong shape/type, field lengths, citations and
missing classifieds have separate fixed rejection categories. The existing 650-character
story-body limit, other field limits, overall response/input bounds and source checks
are unchanged: increased generation headroom is not permission for longer published
copy or unsafe citations, and it may allow more billed reasoning tokens. No automatic
retries or rewrites of rejected output are performed.

The API request uses a named JSON-schema response format, not bare JSON mode with
prompt-only instructions. The root and article objects reject additional properties,
require their exact fields, constrain types and nullable artwork, and limit source
URLs to an enum drawn from that request's verified source records. The local schema
also describes length/count bounds; the OpenAI adapter normalizes unsupported validation
keywords, so deterministic application checks still enforce those numeric limits. This
prevents shape drift at generation time rather than merely detecting it afterwards.
Application-side parsing, duplicate-citation checks, evidence revalidation, privacy
instructions and admin approval remain authoritative even with constrained output.

Gazette author/mention names prefer the server display name. Missing socket-cache
members are looked up through the guild-member REST API; global account names do not
silently substitute for unknown server names. Resolve authors before mentions, at most
64 distinct member IDs per resolution, once per member in that resolution. Unresolved
authors become "A guild member" and unresolved mention names remain unknown. Cited
identities are refreshed through REST during verification and folded into the view hash,
so nickname changes invalidate an old preview just like evidence edits. This lookup is
for editorial names, not an authorization or administrator-membership gate.

Before preview and again before publication, refetch each cited source and compare
author, timestamp, raw-content hash and sanitised-view hash. Edited/deleted messages,
changed identity metadata, revoked access or an unavailable/replaced destination block
publication and require a fresh draft. This verifies cited evidence, not every uncited
context message or the model's interpretation. Permission changes during a Discord
send cannot be made atomic with the external API; checks run immediately before it.

## Approval and delivery

There is one in-memory pending draft, serialised across command instances. A new draft
replaces the earlier approval; a rate-limited request leaves it unchanged. Approval
expires after 30 minutes or restart, and is installed only after the entire private
preview is successfully sent. The random token binds approval to that exact edition
and destination channel ID. No natural-language approval, scheduled event, observer
or public command can publish it.

Approval is consumed before verification/send. An unavailable source or destination
clears it. Only approval checks the bot's View Channel, Send Messages and Embed Links
and Attach Files permissions in the destination; missing posting permissions do not prevent a private
draft. Known pre-send permission failures are explained without implying a send was
attempted. A send failure or timeout may have an ambiguous outcome: do not retry that
approval; inspect `#ai-tavern` before requesting another draft. Concurrent/repeated
approvals cannot send the same pending draft twice. New drafts are not deduplicated
against previously published editions.

Publication is one Discord message containing one or two ordered PNG newspaper pages,
matching image embeds and one "Read text & sources" button. It does not also show the text edition in
the channel. The button returns the exact approved readable edition/source links in
an ephemeral response visible only to the clicking member, never a public follow-up
or DM. All sends use `AllowedMentions.None`. The private admin preview still delivers
all PNG pages and the readable edition together for review. Every page must be delivered
successfully before approval is activated.
`gazette show` reuses those assets; approval never regenerates text, layout or artwork.
Only the edition is published; tokens, approval instructions
and sampling diagnostics stay in the administrator's DM. Draft text and cited evidence
are transient session state until explicitly approved. Logs contain fixed
outcomes/exception types, not chat, generated prose or rejected source bodies.

## Private readable edition button

Only explicitly approved publication snapshots are stored in Mongo's
`gazettePublishedEditions` collection, keyed by the unique publication token. This
retains the final text and source links, guild/channel IDs, approval timestamp and
confirmed message ID, not raw chat evidence, draft history or image bytes. Snapshots
are independent of the bounded issue-number journal, so buttons survive restarts and
later issue publication. Unapproved/discarded previews are never saved here.

Save the snapshot before attempting the Discord send; if persistence fails, do not
send a newspaper with a broken reader button. Confirm the message ID after the send.
An ambiguous send can leave an unconfirmed snapshot; it is readable only through a
matching token on a genuine bot-authored message in the stored guild/channel. It does
not authorize sending again or open a general lookup command.

The gateway acknowledges Gazette buttons ephemerally within Discord's interaction
deadline, then queues the database read. The handler rechecks that the interaction
belongs to the configured connected guild, is on a bot-authored publication in the
stored channel/message, and that the clicking member can currently View Channel and
Read Message History. It uses the interaction's guild-member data, not a separate
member-cache requirement. Recheck access after the database read; wrong-guild/channel,
copied tokens, missing snapshots or outages produce only a private unavailable reply.
The reader shows a historical approved snapshot, not regenerated or newly fetched
source content; Discord itself governs access to the linked source messages. No AI
call or notebook/lore write occurs on a click. Existing messages are not retrofitted
with buttons or archived text.

## Newspaper presentation and issue numbers

Skia renders sharp text, a masthead, cream paper, dark ink, rules/borders and a
classifieds box. The lead is printed in full on page 1. When there are secondary
stories, the front page also has an "Inside this issue" strip with their headlines,
short previews and "Read more · page 2". Page 2 actually exists and contains those
stories in full under "Around the guild", with up to two columns. A single-story
edition has no invented inside page or page-number references. Page numbers and
filenames are assigned deterministically by the renderer, never by the model.
Teasers are single-line, at most 160 characters, and must preview the same sourced
story without introducing new claims. If omitted, the renderer uses a bounded excerpt
of that story's existing body rather than generating additional copy.
The image model only supplies
an illustration, never the text or newspaper layout. The Linux bot image installs
DejaVu fonts; Windows rendering uses Georgia. PNGs are at most 1,200 by 4,000 pixels
with an 8 MB total attachment budget, and complete text wrapping rather than silently clipping copy.
Paragraph breaks in article copy become visible paragraph spacing, included in height
calculations rather than flattened into one block. Unsupported emoji/private-use
display-name decorations are omitted from the PNG to avoid missing-glyph boxes; this
operates on complete Unicode graphemes so joiners/variation selectors are not orphaned.
Readable letters/accents and supported symbols are kept, and the approved text/source
copy retains original display names and decorations unchanged. This does not switch
identities to global usernames or alter stored source evidence. Text remains
available privately through the button for accessibility/mobile reading, and source links remain
clickable in Discord rather than embedded in the PNG.

Edition dates use the guild's Europe/Copenhagen calendar and human-readable date ranges,
without a timezone label next to pure dates. Approval instructions show remaining whole
minutes instead of a UTC timestamp; showing a draft never extends its actual expiry.

Issue numbers start at 1 and live in Mongo's `gazette` state, independently of temporary
drafts. Reading, regenerating, discarding or expiring a draft does not increment them.
Approval atomically reserves the previewed number before attempting a Discord send;
conflicting/stale numbers require a fresh draft. A confirmed message ID acknowledges
the publication. An ambiguous send keeps its reservation to prevent number reuse or
duplicate sends after a restart; exceptional failed attempts can therefore leave a gap.
The bounded journal retains the latest 100 number/token/channel/time/message-ID records,
not raw chat, edition text or artwork. Mongo revision compare-and-swap ensures that
only one competing issue-state writer may claim a revision. Old illustration-attempt
audit entries are retained for compatible document round-trips but no longer limit images.

## Optional illustrations

The writer may propose one small wordless visual joke for the lead story, or omit it
when it adds nothing. Specify an actual visual punchline/contrast, not merely a dwarf
holding the story's object. Art is a simple black-ink editorial cartoon with thick clean
pen contours, large silhouettes, restrained shading and generous empty space: two or
three essential props and at most one anonymous fantasy figure. Detailed workshops,
busy backgrounds and dense engraving/crosshatching are discouraged. Design for the
340-pixel printed thumbnail, not for detail that only reads in a full-size image.
Use objects/anonymous fantasy figures, not identifiable people or photographic evidence.
Illustrations are printed without an explanatory satire caption. Never generate a whole
newspaper with the image model or ask it to typeset the articles.

Generation uses the existing image client but a separate private pipeline: no public
Azure upload, generated-image gallery entry or public confirmation is created. Artwork
is private transient state with the draft/PNG and one short-lived reuse cache. At most
one new illustration request per draft, still subject to the one-minute draft spacing;
there is **no daily illustration quota**. The former three-per-day limit and its
`Gazette:DailyIllustrationLimit` setting have been removed. Disabled art,
generation failures or invalid image data fall back to the text-only newspaper
layout, never prevent the stories/ad from being privately reviewed. The final fallback
page is previewed and approved unchanged.
Every private preview explicitly reports whether art was newly generated, reused,
omitted by the writer, disabled, failed/timed out or failed to render. `gazette show`
retains that explanation; it is never included in the published text or page.
API/client failures now report their numeric HTTP status and whitelisted error-code/type
and parameter labels when available. Unknown labels become unspecified, and raw provider
messages, prompts, response bodies and credentials are never copied into logs/DMs.
Local timeouts are distinguished from API failures; a ClientResultException alone does
not identify the cause. The normal draft command remains unchanged—no targeted-request
or required-image command has been introduced.

Cache one successfully generated and rendered image for at most 30 minutes, keyed by
a hash of the lead's exact verified source records. Redrafts may change wording without
spending another image request when they still propose art for the same evidence.
Edited evidence, source/identity metadata changes, a different source set or expiry
prevent reuse. Reuse does not extend cache expiry. Failed/invalid images are not cached;
discarding a preview may leave the private cache until expiry, and a restart clears it.
Only the hash, image bytes and expiry are cached, not another raw-chat archive. The
administrator reviews reused art as part of the new preview, and normal verification
still runs before both preview and publication.

Gazette artwork still uses the shared `gpt-image-2.5-flare` image client; Sunburst has
not been selected. Only Gazette requests pin `high` quality, 1,024 × 1,024 size and PNG
output instead of provider-selected `auto`. General bot image generation is unchanged.
Pass the explicit `GeneratedImageQuality("high")` value: the SDK's superficially
similar `GeneratedImageQuality.High` constant serializes to legacy DALL·E `"hd"`,
which is not the GPT Image quality value. A transport-level regression test verifies
the actual serialized request, rather than merely comparing SDK constants.
Explicit quality affects token consumption/cost; new requests are billed even when the
administrator discards the edition. Thumbnail drawing uses cubic resampling before monochrome/tinted
printing, avoiding nearest-neighbour aliasing of fine lines.

## Configuration

Optional settings under `Gazette`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `DestinationChannelName` | `ai-tavern` | Exact, case-insensitive name; missing/ambiguous matches fail closed. |
| `DestinationChannelId` | `0` | If set, use this exact channel ID instead of name resolution. |
| `SourceChannelIds` | `[]` | Optional sampling subset; empty means eligible guild text channels, still bounded above. Never bypasses permissions. |
| `IllustrationsEnabled` | `true` | Allow one optional lead-story cartoon per draft. |

The destination must be non-NSFW and normal-member-readable, so its intended audience
can be resolved safely. The bot needs read access to source channels for drafting;
it only needs View Channel, Send Messages, Embed Links and Attach Files in the destination when
publication is approved. Existing Discord, AI and normal-role configuration
is reused; no additional credentials or persistence configuration are introduced.

The committed Production and Development configurations pin the destination by ID
to their existing bot-conversation channel (also included in MentionHandler's channel
configuration and the follower bot-mention exclusion). A renamed/decorated channel
therefore does not depend on an exact `ai-tavern` name match. Destination failures
explain the specific readiness, resolution, normal-role or audience check in
the admin DM rather than combining them into a generic error.

## Validation

Backend tests cover admin-only DM routing, private delivery before approval, replacement,
expiry, discard, concurrent/repeated approval, exact-edition publishing, evidence/access
failures, source access without an admin member-cache entry, private drafting without
posting permissions, approval-only posting checks, draft-specific failure wording,
ambiguous sends, quiet inputs, sampling bounds, REST source filtering and the
writer's tool-free prompt/JSON/citation contracts, server-name resolution, issue-number
reservation/acknowledgement, one-image-per-draft behavior, exact-evidence artwork reuse,
cache expiry and explicit missing-image reasons. Reader tests cover private loading
acknowledgement, ephemeral-only replies, message/audience binding, malformed IDs,
missing snapshots, access revocation and database failures. Publication tests verify
the public payload contains only the ordered images/button and archives approved text before
sending. Preview tests reject approval if even the inside-page delivery fails.
Rendering tests exercise real PNG output, teaser selection and one/two-page composition
and wrapping. Live Mongo compare-and-swap/concurrency checks require a disposable Mongo
instance, and test doubles are not proof of server semantics. Live editorial quality and actual
Discord permissions/delivery require an admin trial after deployment.

`GazetteMongoIntegrationTests` is opt-in: set `COK_GAZETTE_MONGO_TEST_CONNECTION` to a
disposable loopback Mongo server before running the backend tests. It creates/removes
only its own uniquely named test database and verifies competing issue reservations,
durable acknowledgements against the real driver/server.
It also validates published-text persistence across store recreation, duplicate-key
protection and binding an edition to a single confirmed Discord message.
