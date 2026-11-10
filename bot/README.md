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
| Bot guild roster | `Blizzard:ClientId`, `Blizzard:ClientSecret` (required while `GuildRoster:Enabled` is true) |
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

### Guild roster

The roster uses the live Blizzard API and is enabled in the checked-in settings.
The target is EU / Crossroads Mega Chippy. Supply `Blizzard:ClientId` and
`Blizzard:ClientSecret` through the bot's user secrets or environment variables
(`Blizzard__ClientId`, `Blizzard__ClientSecret`). In Development only, missing values
fall back to the existing `cok-forever-api-probe` user-secrets store described below.
There is no runtime mock mode or fixture fallback.

| Setting | Default | Purpose |
| --- | --- | --- |
| `GuildRoster:Enabled` | `true` in appsettings | Enable the live worker, `/leaderboard`, and Grub roster tools |
| `GuildRoster:RefreshSeconds` | `60` | Roster check interval; minimum 10 seconds |
| `GuildRoster:ProfessionRefreshSeconds` | `600` | Separate profession check interval; minimum 10 seconds |
| `GuildRoster:MilestoneChannelId` | Development `1092927596938924054`; Production `1567646266689851402` | Milestone destination text channel ID in `Bot:GuildId` |
| `GuildRoster:LevelCap` | `60` | Character cap milestone and level celebration target; null disables cap celebrations |
| `GuildRoster:ProfessionMilestones` | `[75,150,225,300]` | Ordinary profession rank milestones; 300 is always included |
| `Blizzard:Region` | `eu` | Regional API host and Forever namespaces |
| `Blizzard:GuildName` | `Crossroads Mega Chippy` | Expected in-game guild name |
| `Blizzard:GuildSlug` | `crossroads-mega-chippy` | Guild route identifier |
| `Blizzard:RosterPathTemplate` | `/data/wow/guild/{guild}/roster` | Expected realm-free membership route |
| `Blizzard:ProfessionPathTemplate` | `/profile/wow/character/{first}-{last}/professions` | Fallback character-professions route when no profile link is returned |

Use a Development Discord guild and disposable Mongo database for local trials;
`Bot:GuildId` supplies the Discord scope. After the game/API publish the guild, start
the bot and check `/leaderboard`, then a profession filter if supported. The first
successful snapshot sends no milestones. Configure channel permissions before a
character crosses a milestone. Inspect safe status logs for routing/schema failures.
To stop collection, tools, leaderboard registration and delivery, set
`GuildRoster__Enabled=false` and restart. See
[runtime behavior and launch debugging](docs/guild-roster-runtime.md).

### Forever API probe

The standalone probe uses .NET 10 and does not start the bot or access MongoDB/Discord.
Create a client at [Battle.net API Access](https://develop.battle.net/access/clients).
Store its credentials in the probe's dedicated local user-secrets store (PowerShell 7):

```powershell
dotnet user-secrets set --id cok-forever-api-probe "Blizzard:ClientId" (Read-Host "Client ID")
dotnet user-secrets set --id cok-forever-api-probe "Blizzard:ClientSecret" (Read-Host "Client secret" -MaskInput)
```

The client ID is public; only the client secret is confidential. User secrets live outside
the repository but are not encrypted. The existing bot user-secrets store is unchanged.
Alternatively, supply `COK_PROBE_Blizzard__ClientId` and
`COK_PROBE_Blizzard__ClientSecret` as environment variables; they override user secrets.
Never put the secret in committed settings, chat, or command-line arguments.

Run from `bot/`:

```sh
dotnet run --project src/ChampionsOfKhazad.Bot.BlizzardProbe -- --region eu
dotnet run --project src/ChampionsOfKhazad.Bot.BlizzardProbe -- --region eu --guild crossroads-mega-chippy
dotnet run --project src/ChampionsOfKhazad.Bot.BlizzardProbe -- --help
```

Optional public targets: `--character firstname-surname` probes the reported realm-free
character/profile-professions paths; `--guild guild-slug` probes realm-free guild
and roster paths. Optional `--realm internal-realm-slug` selects the older route form
only when investigating actual internal routing. Use known target slugs rather
than fabricated names. `--static-build 1.60.1_70399` overrides the candidate static build
(the displayed value is also the default). Regions: `eu` (default), `us`, `tw`, `kr`.
No URLs or credential arguments are accepted.

Output contains HTTP statuses, selected cache headers, and allowlisted JSON shape/count
summaries, not tokens or response bodies. The probe makes one sequential pass with no
retries and stops on unexpected HTTP errors, including 401/429/5xx and redirects.
403/404 are recorded observations; exit code zero means the pass completed, **not**
that guild/profile support works. Exit code one means setup/authentication/transport
failure or an interrupted pass. Nothing is imported, saved to MongoDB, or announced.
See [current evidence and interpretation](docs/forever-api-research.md).

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

The live roster needs two additional Pulumi values before the next deployment.
From `bot/src/ChampionsOfKhazad.Bot.Infrastructure/`, set them without putting the
secret in shell history (PowerShell 7):

```powershell
pulumi config set blizzardClientId (Read-Host "Client ID") --stack prod
pulumi config set --secret blizzardClientSecret (Read-Host "Client secret" -MaskInput) --stack prod
```

Pulumi supplies the ID as `Blizzard__ClientId` and stores the secret in the bot
Container App's secret collection, referenced by `Blizzard__ClientSecret`. The portal
does not receive these credentials. Local user secrets are not deployed automatically.
