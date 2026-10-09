# Background notebook observer

**Status: implemented; production observation enabled on deployment.**

## Problem Statement

The original temporary notebook discovered memories only while answering direct mentions.
Guild shenanigans, useful game observations, and memorable anecdotes can happen without
anyone addressing the Lorekeeper. The bot should occasionally notice those conversations
without interrupting them or requiring members to request a note.

## Solution

A silent observer is driven by the existing event loop and MTTH mechanism. It reads bounded
batches of new human messages from channels allowed by the existing normal-member access
policy, then proposes zero or more genuinely noteworthy, distinct notebook entries.

Each proposal uses the existing notebook evidence verification, independent review,
canon/conflict checks, expiry, and admin review-DM pipeline. A scan has no target number
of notes to fill and no arbitrary one-note cap. Its work is bounded by the input batch
and available observer/guild budgets.

This is opportunistic discovery, not an exhaustive archive or guaranteed capture of every
noteworthy event. MTTH's random gaps are acceptable for this purpose.

## User Stories

1. As a guild member, I want memorable incidents noticed without mentioning the bot, so that useful anecdotes can be recalled later.
2. As a guild member, I want observation to be silent in guild chat, so that conversations are not interrupted by bot responses.
3. As a guild member, I want ordinary conversations to produce no notes when nothing qualifies, so that generic banter does not become memory.
4. As a guild member, I want several distinct noteworthy incidents to produce several notes, so that one incident does not crowd out another in the same scan.
5. As a guild member, I want related messages about one incident combined as supporting evidence, so that the notebook does not accumulate duplicate memories.
6. As a guild member, I want firsthand accounts retained as attributed reports, so that an anecdote is not promoted to independently verified fact.
7. As a guild member, I want existing channel-access restrictions preserved, so that officer/private conversations are not observed or leaked through later lookups.
8. As an admin, I want independently accepted notes delivered through the existing review DMs, so that I can inspect or discard background memories.
9. As an admin, I want notebook pause to stop background observation too, so that there is one effective control over new memory collection.
10. As an admin, I want bounded observation costs and review volume, so that scanning does not exhaust the interactive notebook budget or flood my DMs.
11. As an admin, I want notes identified as background discoveries, so that they are not attributed to a member who never requested them.
12. As an admin, I want visibility into observer activity and budget use without archived raw chat, so that I can diagnose inactivity while preserving privacy.

## Implementation Decisions

### Reuse existing channel scope

- Reuse `NormalUserChannelAccess` and the existing Discord message-access policy rather
  than introduce a second definition of public channels or a required separate allowlist.
- Here, "public" means readable by ordinary guild members under the configured normal-user
  role, not necessarily readable by `@everyone`. The bot must also be able to read the channel.
- Member-readable NSFW text channels are included, as explicitly chosen for this feature.
  Threads and voice channels are excluded; observation does not add a broader channel scope.
- Preserve the existing exclusion of officer/private channels and the stricter audience
  filtering used when replying in an `@everyone`-readable channel.
- Expose background channel access explicitly while sharing permission calculations with
  interactive tools. Do not fabricate an admin request to bypass requester-oriented APIs.
- Recheck access when reading evidence and when recalling notes; eligibility at an earlier
  scan is not permanent permission to use a channel's content.
- Disconnected clients and obsolete guild contexts after reconnect fail closed for background reads.

### Reuse the event system

- Add a notebook observation event to the existing eligibility/MTTH/fire lifecycle. Do not
  introduce another scheduler or redesign MTTH for this feature.
- Eligibility checks bot readiness, notebook pause, cooldown, and available budgets.
  Discord batch reads and model calls belong in the firing work, not eligibility checks.
- MTTH controls the chance of firing at an eligible tick; it is not a guaranteed scan
  interval. Cooldown adds spacing beyond the MTTH roll.
- A completed scan counts for cooldown even when no candidate qualifies. Empty results
  are successful observation, not a reason to immediately try again.
- Keep observation work bounded so it cannot monopolize the sequential event loop.
- Gate event resolution on Discord readiness, including disconnects, before constructing
  scoped events that depend on the guild context. Event scopes are asynchronously disposed.
- Restore an established guild context through connected/guild-available signals on session
  resumption: Discord.Net can resume without raising `Ready`, and can announce availability
  before restoring `CurrentUser`. Both connection and guild availability must be usable;
  initial startup still waits for the first `Ready`.

### Observer and scan progress

- A small observer service owns batch selection, candidate discovery, budgets, and progress.
  The event remains orchestration rather than embedding the full notebook workflow.
- Read bounded batches of new human messages and skip model calls when there is no new
  eligible content. Ignore bot/webhook messages as notebook evidence.
- Maintain per-channel progress outside event-instance fields: the event loop recreates
  its scoped event instances each tick. Do not repeatedly rediscover the same consumed chatter.
- Persist checkpoints and a time-limited active-scan lease with notebook state. Concurrent
  firings cannot both claim the same scan, and an expired lease permits restart recovery.
- Carry scan ownership through background review reservation, commit, and activation CAS
  updates. An expired/replaced scan cannot finish a note merely because its cancellation timer
  has not fired yet. Ownership is rechecked before the review DM; loss during delivery leaves
  the persisted note audit-only rather than making it usable memory.
- Notebook pause expires the active scan lease immediately. Resume does not revive that lease;
  an older scan cannot activate a note after a quick pause/resume during DM delivery.
- Do not persist message bodies as an additional chat archive. Checkpoints and diagnostics
  should contain identifiers, timestamps, and fixed outcomes rather than raw conversation.
- Give the model an observation-specific task, not an artificial user query or the full
  interactive personality/tool set. All observed content remains untrusted data, not instructions.
- The model may propose zero or multiple distinct candidates. Do not force a minimum,
  set a target to fill, or impose a one-note-per-scan restriction.

### Existing notebook pipeline remains authoritative

- Group evidence for the same incident instead of producing multiple paraphrases.
- Process candidates sequentially through the existing notebook pipeline so later reviews
  see earlier committed notes and their duplicate/conflict state.
- Each candidate independently requires verified human source messages from safe channels
  within seven days. Existing content limits, privacy rules, canon precedence, expiry,
  capacity limits, and review-DM activation requirements remain in force.
- A rejected candidate is not retried or rephrased. Its rejection does not prevent unrelated
  candidates from being considered while the notebook remains enabled and budget allows.
- Record explicit background provenance. Do not charge the last speaker or another arbitrary
  member as though they requested the note.
- Preserve the shared guild budgets and define a bounded observer allowance. Observation
  model calls need their own budget: an empty discovery call still costs money, whereas the
  current evaluation ledger accounts for review reservations, not discovery calls.
- Keep the one-attempt-per-interactive-request guard unchanged. Background proposals need
  their own entry point rather than weakening the interactive tool's spam protection.
- Preserve existing admin-only diagnostics and controls. Background observation itself
  sends no public messages; accepted notes still generate the normal admin review DMs.

## Testing Decisions

Test observable behavior with the existing xUnit/test-double approach, without requiring
live Discord, MongoDB, or AI credentials:

- Channel selection shares normal-member permission rules, excludes restricted channels,
  and preserves audience-safe lookup behavior after permissions change.
- Event eligibility respects readiness, pause, cooldown, and budgets; completed empty scans
  advance cooldown without creating notes or making public responses.
- Bounded scan progress survives event-instance recreation, avoids reconsidering consumed
  messages, and does not call the model when there is no new eligible content.
- Discovery accepts an empty candidate list or multiple distinct candidates without a
  forced target; related evidence supports one incident rather than duplicate proposals.
- Sequential processing makes earlier accepted notes visible to later reviews. Rejection of
  one candidate does not block unrelated candidates or permit retries of the same memory.
- Existing source, privacy, canon, quota, stale-review, and failed-DM safeguards also apply
  to background-origin notes; members are not charged for unsolicited observation.
- Discovery and review budgets remain distinct, including calls producing no candidates,
  failures, partial completion, and cancellation. Diagnostics do not expose raw chat.

Reuse the existing notebook service/evaluator, channel access-policy, admin-command, and
serialization tests as prior art. These tests validate mechanics and supplied instructions,
not the live model's judgment of what is noteworthy; trial operation needs admin review.

## Out of Scope

- A new scheduler, precise scan timing, or guaranteed capture of every guild event.
- Exhaustive historical backfill or indefinite storage of observed conversations.
- A separate mandatory channel allowlist duplicating the current access policy.
- Officer/private-channel observation, canon editing, personal profiles, or relaxed evidence/privacy rules.
- Public commentary, reactions, generated images, or other actions taken by the observer.
- Changes to interactive notebook attempt limits.

## Further Notes

### Configuration and initial allowances

Settings live under `EventLoop:NotebookObservation`. Production explicitly enables observation;
Development disables it by default. The options class supplies the following defaults:

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Allow scans; notebook pause still takes precedence. |
| `MeanTimeToHappenMinutes` | `60` | Chance-based scheduling, not a guaranteed interval. |
| `CooldownMinutes` | `30` | Minimum spacing after a completed scan, including empty/failed scans. |
| `LookbackMinutes` | `60` | Maximum recent-chat window on initialization and catch-up. |
| `DailyDiscoveryLimit` | `12` | Rolling 24-hour discovery model-call reservations. |
| `DailyReviewLimit` | `10` | Background review reservations, also subject to the guild limit of 30. |
| `DailyNoteLimit` | `5` | Background recorded notes, also subject to the guild limit of 10. |
| `MaximumChannelsPerScan` | `4` | Channels examined per firing. |
| `MaximumMessagesPerChannel` | `20` | Maximum raw messages fetched per selected channel. |
| `MaximumInputCharacters` | `20000` | Total serialized source characters supplied to discovery. |
| `ScanTimeoutSeconds` | `180` | Deadline for reads, discovery, and sequential candidate processing. |

An otherwise idle 15-minute event-loop tick with a 60-minute MTTH gives a 25% chance of firing.
The roll uses actual elapsed time, so event work can increase that interval/probability.
Cooldown adds spacing; these values do not promise one scan per hour.
Budgets are rolling 24-hour limits, not midnight resets. Discovery slots are reserved
before the call; empty responses, invalid responses, failures, and cancellations count.
Background reviews and notes share guild limits but do not consume a member's personal budget.

Discovery and independent review explicitly use high reasoning effort on the shared Luna
text model. Discovery does not set an explicit output-token cap; response validation,
workflow budgets, and the 180-second production scan deadline remain unchanged. The deadline
still covers discovery plus sequential reviews. Extra reasoning can consume scan time;
watch invalid discovery responses and partial/timed-out scans before changing these limits.

### Progress, partial work, and failure behavior

- Channels with the oldest selection attempts are visited first, rotating tied priorities
  using each channel's least-recent first-input priority, including across different selection
  cohorts. Attempts are persisted separately from message cursors, including selections
  that fail to read, so failed channels cannot starve healthy ones. Model input interleaves
  messages across selected channels instead of letting the first channel fill every batch.
  Checkpoints contain only channel ID, high-water message ID, and successful scan time;
  selection metadata contains channel IDs and attempt times, not message content.
- Each read starts after the later of its persisted cursor and the one-hour lookback boundary.
  Downtime does not trigger exhaustive backfill; busy channels and input limits can cause sampling.
- A successful discovery consumes the fetched batch before reviewing candidates, even if it
  proposes nothing or some candidates are rejected. Bot-only/otherwise ineligible batches can
  advance checkpoints without a discovery call. Fresh permissions filter discovery input and
  candidate evidence; notebook control changes during discovery prevent the batch from being
  committed as consumed. Consuming a batch never grants permission to use its notes later.
- Read failures leave that channel's cursor unchanged. Discovery failure leaves batch cursors
  unchanged. Retrying unprocessed work remains bounded by the current lookback window and budgets.
- The model orders candidates by usefulness. Processing stops when budgets/capacity/pause or
  operational failures prevent further writes. Remaining candidates are not persisted for replay;
  a crash or exhausted budget can therefore lose discoveries. This is intentional opportunistic
  observation, not guaranteed delivery of every candidate.
- Rejected candidates are not retried or rephrased from that discovery batch. Existing exact-text,
  source-reuse, and independently reviewed duplicate/conflict safeguards remain authoritative.
- Observer state updates do not change the notebook's review revision. Writes and admin controls
  still invalidate stale reviews through the existing pipeline.
- Completed scan diagnostics retain at most 100 outcomes within 24 hours: timestamps, outcome,
  message/proposal/attempt/saved counts, and read-failure count. They do not retain raw chat or
  rejected candidates. Completion recording is best-effort; an abandoned active scan expires
  through its lease rather than permanently blocking observation.

For current, implemented notebook behavior, see [Lorekeeper's temporary notebook](notebook.md).
