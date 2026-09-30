## Permissions

- Read Messages/View Channels
- Send Messages
- Send Messages in Threads
- Read Message History
- Manage Messages

Bitmask: 274877983744

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
