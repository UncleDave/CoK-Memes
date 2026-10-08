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

The default window is the preceding seven days. A successful edition contains one to
three sourced stories and, optionally, a tiny fictional advert/editor's note explicitly
labelled satire. Quiet or unsuitable conversation produces no edition, not invented
news or padding. No notebook entries are required or written; no lore is changed.
Archival lore callbacks are not included in this first version.

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

- Require the configured normal-member role, administrator and bot to be able to read
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
have a three-minute deadline.

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
clears it. A send failure or timeout may have an ambiguous outcome: do not retry that
approval; inspect `#ai-tavern` before requesting another draft. Concurrent/repeated
approvals cannot send the same pending draft twice. New drafts are not deduplicated
against previously published editions; there is no persistent edition archive yet.

Publication is one Discord embed with at most 4,000 description characters and
`AllowedMentions.None`. Only the edition is published; tokens, approval instructions
and sampling diagnostics stay in the administrator's DM. Draft text and cited evidence
are transient session state, not an additional database archive. Logs contain fixed
outcomes/exception types, not chat, generated prose or rejected source bodies.

## Configuration

Optional settings under `Gazette`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `DestinationChannelName` | `ai-tavern` | Exact, case-insensitive name; missing/ambiguous matches fail closed. |
| `DestinationChannelId` | `0` | If set, use this exact channel ID instead of name resolution. |
| `SourceChannelIds` | `[]` | Optional sampling subset; empty means eligible guild text channels, still bounded above. Never bypasses permissions. |

The destination must be non-NSFW, normal-member/admin/bot-readable, and the bot must
have Send Messages and Embed Links. Existing Discord, AI and normal-role configuration
is reused; no additional credentials or persistence configuration are introduced.

## Validation

Backend tests cover admin-only DM routing, private delivery before approval, replacement,
expiry, discard, concurrent/repeated approval, exact-edition publishing, evidence/access
failures, ambiguous sends, quiet inputs, sampling bounds, REST source filtering and the
writer's tool-free prompt/JSON/citation contracts. Live editorial quality and actual
Discord permissions/delivery require an admin trial after deployment.
