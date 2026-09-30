## Permissions

- Read Messages/View Channels
- Send Messages
- Send Messages in Threads
- Add Reactions
- Read Message History
- Manage Messages

Bitmask: 274877983808

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
