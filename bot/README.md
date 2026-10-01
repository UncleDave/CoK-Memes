# Champions of Khazad bot

A Discord bot for the Champions of Khazad guild, with guild lore, AI responses,
memes and raid sign-ups. An ASP.NET Core/React portal provides access to guild lore
and generated images.

## Requirements

- .NET 10 SDK
- Node.js 24 and npm for the portal frontend
- For execution: Discord, MongoDB, OpenAI, Azure Storage and Raid Helper configuration;
  the portal also requires Auth0

Building and running tests does not require service credentials.

## Project layout

- `ChampionsOfKhazad.Bot.slnx` — the .NET solution
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

### Discord setup

Enable **Server Members Intent** and **Message Content Intent** in the Discord Developer
Portal. Invite the bot with the `bot` and `applications.commands` scopes and these permissions:

- Read Messages/View Channels
- Send Messages
- Send Messages in Threads
- Add Reactions
- Read Message History
- Manage Messages

Permission bitmask: `274877983808`.

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
The [deployment workflow](../.github/workflows/bot-deploy.yml) builds, tests and deploys
the production stack on matching pushes to `main`. Supply deployment credentials through
Pulumi configuration and GitHub secrets, not committed application settings.
