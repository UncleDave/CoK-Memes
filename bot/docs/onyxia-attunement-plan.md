# Onyxia attunement tracker

Draft plan, 26 November 2026. Not implemented.

## Proposed pilot

Of the two raid days, only the one hosting Onyxia needs an attunement summary.
An officer selects the exact Raid Helper signup to track, rather than configuring
a weekday or matching an event title. Grub posts one reply to that signup and
edits it in place. Re-enabling tracking or restarting the bot should not create
another reply. The other raid day's signup gets no message.

Raiders see aggregate counts and when they were last checked, with no names or
pings. For example:

> **Onyxia attunement**
>
> 🟢 25 complete · 🟡 7 in progress · 🔴 3 no steps recorded · ⚪ 5 unknown
>
> Checked 12 minutes ago

An **Officer details** button on the same reply opens a private Discord response.
Check the configured Discord officer role and guild on each request. List signups
with usable quest data that does not show the final quest, with their character
and last recorded step where available. Put missing character selections and
unreliable data under **Needs verification**, not in the list of raiders who need
quest work. Do not copy named results to channel history, lore, or the notebook.

## Character and quest evidence

Check the character the player is bringing, not a Discord display name or a main
guessed from lore. The first workflow to try is a Raid Helper signup note such
as `Char: First Last`, matched exactly to the configured guild roster. The note
selects a character for this event; it does not prove Battle.net ownership. If
notes are unsuitable, use an explicit per-event character selection instead.

Use the [Forever completed-quests feed](https://api.ulminia.net/endpoints/forever-character-quests-character-completed-quests)
to check the faction-specific chain. For the guild's Horde pilot, the
[final quest is Blood of the Black Dragon Champion](https://www.wowhead.com/forever/quest=6602/blood-of-the-black-dragon-champion)
(`6602`). Verify the chain IDs and coverage in actual API responses before relying
on a missing quest ID. Blizzard's [raid guide](https://news.blizzard.com/en-us/article/24303009/world-of-warcraft-forever-raid-overviewlocations-raid-sizes-and-attunement)
requires carrying the Drakefire Amulet; quest completion does not prove it is
still in the character's bags. The tracker never gates signups.

Use green, yellow, and red only when the selected character has a usable,
sufficiently fresh quest list.

| State | Evidence |
| --- | --- |
| 🟢 Quest complete | The final quest is recorded. |
| 🟡 In progress | Earlier chain steps are recorded, but the final quest is not. |
| 🔴 No steps recorded | None of the tracked steps are recorded. |
| ⚪ Unknown | No unambiguous character selection, or the quest data is missing, unverified, or stale. |

## Refresh and boundaries

- Start with a check at attachment, then about every 30 minutes and once shortly
  before raid time, subject to upstream cache and backoff. Follow reschedules and
  stop at the scheduled start or confirmed cancellation, not at signup close.
- Bind the Raid Helper event ID to the original signup message and the Grub reply
  separately. Use the latest signup list for that event; edit only Grub's own
  reply. A failed refresh must not turn a roster into zero signups or be treated
  as evidence of non-completion. Label retained results when stale rather than
  advancing their freshness timestamp on a failed check.
- Keep the integration read-only towards Raid Helper. Its [v4 event list docs](https://raid-helper.dev/documentation/api#get-events)
  describe optional signups and notes, but our list client currently deserialises
  pagination metadata only. The pilot needs to verify and read the actual signup
  payload. It does not depend on restoring the [deregistered `/raids` command](raid-signups.md).

## Before enabling it

1. Use a test signup to confirm the event-to-message link, start time, signup
   categories, and character notes with a main and an alt.
2. Verify quest responses against known completed, in-progress, and not-started
   cases. Check how long a turn-in takes to show up and how a missing or incomplete
   profile differs from a usable list.
3. Test that refresh, restart, reschedule, cancellation, and upstream failure do
   not duplicate the reply or mislabel raiders. Verify that only officers can
   get named results. Then try one real Onyxia signup.

## Still to refine

- Attachment controls and which Discord role can manage tracking or view details.
- Which Raid Helper signup statuses count, especially tentative, late, and bench.
- The character selection fallback, quest freshness threshold, and timing for
  the last check once upstream behaviour is measured.
- What to leave visible after raid start and how long to retain event data.
