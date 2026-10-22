# Raid sign-ups

`/raids` is de-registered independently of the guild rebrand. At startup, the bot
registers enabled guild commands and removes its existing `/raids` slash-command
registration, if present. The optional roster feature separately manages its own
`/leaderboard` slash registration according to `GuildRoster:Enabled`. Other
unrelated command names and command types are not removed.
`/summarise` and `/suggest` remain registered.

The command definition, handler, Raid Helper client, configuration, and old raid
jokes remain in the codebase for possible level-60 use. No channels or sign-ups
are cleared by de-registering the command.

## Before restoring the command

The retained implementation is not configured for Forever. Review all of:

- Raid Helper template (`wowmop`) and raid title (Siege of Orgrimmar).
- Wednesday reset assumption, Sunday/Monday raid days, and 19:30 start time.
- The 210-minute duration, configured channels, role mentions, and permissions.

The handler clears configured sign-up channel messages before creating events.
Review that destructive behavior before making the command available again.

Once it is ready, add `SlashCommands.Raids` back to `GuildCommands`. Startup cleanup
skips the raid command when it is enabled there.
