# Grub

A Discord bot for the Crossroads Mega Chippy guild, with guild lore, AI responses,
memes and raid sign-ups. An ASP.NET Core/React portal provides access to guild lore
and generated images.

## Requirements

- .NET 10 SDK
- Node.js 24 and npm for the portal frontend
- For execution: Discord, MongoDB, OpenAI, Azure Storage and Raid Helper configuration;
  the portal also requires Auth0

Building and running the default test suite does not require service credentials, MongoDB, or Docker.

## Project layout

- `ChampionsOfKhazad.Bot.slnx` — the .NET solution
- `src/Directory.Build.props` — shared .NET framework, nullable and implicit-using defaults
- `src/ChampionsOfKhazad.Bot/` — the Discord host
- `src/ChampionsOfKhazad.Bot.Portal/` — the portal backend and `frontend/` React client
- Other `src/ChampionsOfKhazad.Bot.*` projects — feature libraries, persistence and tests
- `src/ChampionsOfKhazad.Bot.Infrastructure/` — Pulumi deployment infrastructure
- `../isleafanofficeryet/` — an independent static site

## Build and test

Run from `bot/`:

```sh
dotnet tool restore
dotnet build ChampionsOfKhazad.Bot.slnx
dotnet test --solution ChampionsOfKhazad.Bot.slnx --no-build --minimum-expected-tests 1
dotnet csharpier check .
```

Use `dotnet csharpier format .` to format C# changes.

CI tests check HTTP/service contracts and the commands sent by the Mongo adapter using narrow test doubles.
They do not simulate or prove MongoDB index, collation, or concurrency behavior; validate those separately
against a disposable local MongoDB instance when changing persistence behavior. Live database checks are
opt-in and are not part of the default test command or CI.

Run frontend commands from `bot/src/ChampionsOfKhazad.Bot.Portal/frontend/`:

```sh
npm ci
npm run build
npm test
npm run lint
```

The frontend request and action tests use Node's built-in test runner and do not require service credentials.

## Local configuration

Both hosts use .NET configuration: `appsettings.json`, environment-specific settings,
user secrets in Development, and environment variables. Keep credentials in user secrets
or environment variables, never in committed settings files. Use `__` in environment
variable names in place of `:` (for example, `ConnectionStrings__Mongo`).

The hosts have separate user-secret stores. From `bot/`, for example:

```sh
dotnet user-secrets set "Bot:Token" "<discord-bot-token>" --project src/ChampionsOfKhazad.Bot
dotnet user-secrets set "ConnectionStrings:Mongo" "<mongo-connection-string>" --project src/ChampionsOfKhazad.Bot
dotnet user-secrets set "ConnectionStrings:Mongo" "<mongo-connection-string>" --project src/ChampionsOfKhazad.Bot.Portal
```

Configure the remaining values before starting either host:

| Host | Configuration |
| --- | --- |
| Bot | `Bot:Token`, `Bot:GuildId`, `ConnectionStrings:Mongo`, `OpenAIServiceOptions:ApiKey`, `MediatR:LicenseKey`, `RaidHelper:ApiKey` |
| Bot storage | `AzureStorageAccountName`, `AzureStorageAccountAccessKey` |
| Bot error logging | `DiscordSerilogSink:WebhookId`, `DiscordSerilogSink:WebhookToken` |
| Portal | `BotToken`, `GuildId`, `ConnectionStrings:Mongo`, `OpenAi:ApiKey` |
| Portal authentication | `Auth:Domain`, `Auth:ClientId`, `Auth:ClientSecret`, `Auth:AdminUserIds` |

Review the bot's existing `appsettings*.json` files for guild-specific channel/user IDs
and feature settings; replace them with values for your development guild.

The on-demand Gazette uses the existing admin DM identity, AI configuration and
normal-member role. Its default publication channel is `#ai-tavern`; the bot also
needs Embed Links and Attach Files there to publish, but not to prepare a private draft. Optional
`Gazette:DestinationChannelId` and
`Gazette:SourceChannelIds` pin the destination and narrow sampling. See
[Gazette configuration and DM commands](docs/gazette.md) before a live trial.
The bot's Linux image installs DejaVu fonts for rendered newspaper pages. Issue numbers
and published-text snapshots use the existing Mongo configuration. Gazette images have
no daily quota; repeated drafts of unchanged lead evidence can reuse recent artwork.

### Discord setup

Enable **Server Members Intent** and **Message Content Intent** in the Discord Developer
Portal. Invite the bot with the `bot` and `applications.commands` scopes and these permissions:

- Read Messages/View Channels
- Send Messages
- Send Messages in Threads
- Embed Links
- Attach Files
- Add Reactions
- Read Message History
- Manage Messages

Permission bitmask: `274878032960`.

For the rebrand rollout and external Discord profile changes, see
[Guild identity and Grub](docs/guild-identity.md#rollout).

### Mock guild roster

The roster feature is off by default and currently has only a file-backed mock
source. The normal bot credentials are still required to run the host; no Blizzard
credentials are needed. Use a development guild and disposable Mongo database.

| Setting | Default | Purpose |
| --- | --- | --- |
| `GuildRoster:Enabled` | `false` | Enable the worker, `/leaderboard`, and Grub roster tools |
| `GuildRoster:MockFile` | `Fixtures/guild-roster.mock.json` | Roster fixture; relative to the bot output/publish directory, or an absolute path |
| `GuildRoster:RefreshSeconds` | `60` | Roster check interval; minimum 10 seconds |
| `GuildRoster:ProfessionRefreshSeconds` | `600` | Separate profession check interval; minimum 10 seconds |
| `GuildRoster:MilestoneChannelId` | unset | Optional destination text channel ID in `Bot:GuildId` |
| `GuildRoster:AllowMockNotifications` | `false` | Additional opt-in required to send mock notices |
| `GuildRoster:LevelCap` | unset | Optional additional character-level milestone |
| `GuildRoster:ProfessionMilestones` | `[75,150,225,300]` | Profession rank milestones |

For an editable local trial, copy `src/ChampionsOfKhazad.Bot/Fixtures/` to a local
directory and set `MockFile` to its roster file's absolute path. Keep the sibling
`professions/{character-id}.json` files with it. Configure settings through user
secrets or environment variables, as above; `Bot:GuildId` supplies the Discord scope.

1. Enable the feature and start the bot. `/leaderboard` should show **Mock roster**;
   `/leaderboard profession:alchemy` should show the sample profession rank.
2. If testing notices, set the private test channel and allow mock notifications
   **before** the first collection. The first collection should send nothing.
3. Change Borin's level from 19 to 20 in the roster response; after a refresh,
   expect a labelled mock milestone. Change Cooking from 74 to 75 in
   `professions/1001.json` for a secondary-profession milestone. Wait for the
   respective configured intervals, or use ten-second intervals for this trial.
4. Remove a character from `members` and confirm queries omit it. Restart the bot
   against the same database and confirm sent notices are not queued again.

Do not edit the output-directory fixtures if you need changes to survive a build;
the next build may replace them with the committed examples. No real API client
is enabled by these settings. See [roster behaviour and API assumptions](docs/guild-roster-runtime.md).

## Run locally

From `bot/`, start the Discord bot:

```sh
dotnet run --project src/ChampionsOfKhazad.Bot
```

For the portal, install frontend dependencies first, then run from `bot/`:

```sh
dotnet dev-certs https --trust
dotnet run --project src/ChampionsOfKhazad.Bot.Portal
```

The portal's launch profile uses `https://localhost:7208`. Its Development configuration
proxies the frontend to Vite at `http://localhost:5173`; the SPA integration can start
Vite, or you can run `npm run dev` from the frontend directory. Use the portal URL when
exercising authenticated flows, and configure Auth0's local application URLs accordingly.

The included launch profiles select Development, so local user secrets are loaded.

## Deployment

Deployment is managed by Pulumi in `src/ChampionsOfKhazad.Bot.Infrastructure/`.
Frontend tests, lint and the production build run on Node.js 24 and must pass before deployment starts.
The [deployment workflow](../.github/workflows/bot-deploy.yml) builds, tests and deploys
the production stack on matching pushes to `main`. Supply deployment credentials through
Pulumi configuration and GitHub secrets, not committed application settings.
