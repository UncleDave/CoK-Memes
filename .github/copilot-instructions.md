# Champions of Khazad Discord Bot

Champions of Khazad is a Discord bot application with a web portal, built with .NET 9, Node.js/TypeScript frontend, and deployed to Azure Container Apps. The repository contains both the main Discord bot and a simple static website.

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Working Effectively

### Bootstrap and Build the Repository
- Install .NET 9 SDK (required):
  ```bash
  wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && chmod +x dotnet-install.sh && ./dotnet-install.sh --channel 9.0
  export PATH="$HOME/.dotnet:$PATH"
  ```
- Verify .NET version: `dotnet --version` (should show 9.x)
- Install Node.js 24 (frontend uses Vite with React/TypeScript)
- Navigate to bot directory: `cd bot`
- Restore .NET tools: `dotnet tool restore` (installs CSharpier formatter)
- **NEVER CANCEL**: Build .NET solution: `dotnet build ChampionsOfKhazad.Bot.slnx` -- takes 35 seconds. Set timeout to 120+ seconds.
- **NEVER CANCEL**: Build frontend: `cd src/ChampionsOfKhazad.Bot.Portal/frontend && npm ci && npm run build` -- npm install takes 15 seconds, build takes 15 seconds. Set timeout to 300+ seconds total.

### Development Workflow
- **Frontend Development**: 
  - `cd bot/src/ChampionsOfKhazad.Bot.Portal/frontend`
  - `npm run dev` (starts Vite dev server on http://localhost:5173/)
- **Backend Development**: The Discord bot requires external dependencies (Discord token, MongoDB, OpenAI API, etc.) that cannot be run locally without secrets
- **Local Testing**: You can build and validate code changes but cannot run the full bot without production secrets

### Linting and Formatting
- **C# Code**: `dotnet csharpier check .` (in bot directory) - reports formatting issues
- **C# Auto-fix**: `dotnet csharpier format .` (in bot directory) - auto-formats code
- **Frontend Code**: `npm run lint` (in frontend directory) - runs ESLint and Prettier check, takes 2 seconds
- **ALWAYS** run linting before committing or CI will fail (.github/workflows/bot-build.yml)

## Validation
- **Always manually validate .NET builds** by running `dotnet build ChampionsOfKhazad.Bot.slnx` after making C# changes
- **Always manually validate frontend builds** by running `npm run build` in the frontend directory after making TypeScript/React changes
- **Always run linting tools** before finishing work to ensure CI passes
- **Test frontend UI changes** by running `npm run dev` and visiting http://localhost:5173/
- **Docker builds work** but require production secrets to run fully - test locally for build validation only

## Common Tasks

### Repository Structure
```
/
├── .github/
│   └── workflows/           # CI/CD pipelines
├── bot/                     # Main Discord bot project
│   ├── .config/dotnet-tools.json  # CSharpier configuration
│   ├── .csharpierrc.json    # C# formatting rules
│   ├── ChampionsOfKhazad.Bot.slnx  # Solution file
│   └── src/
│       ├── ChampionsOfKhazad.Bot/           # Main bot executable
│       ├── ChampionsOfKhazad.Bot.Core/      # Core utilities
│       ├── ChampionsOfKhazad.Bot.Portal/    # Web portal (ASP.NET Core)
│       │   └── frontend/    # React/TypeScript frontend
│       ├── ChampionsOfKhazad.Bot.GenAi/     # AI integrations
│       ├── ChampionsOfKhazad.Bot.Lore/      # Guild lore system
│       ├── ChampionsOfKhazad.Bot.DiscordMemes/ # Meme functionality
│       ├── ChampionsOfKhazad.Bot.RaidHelper/   # Raid scheduling
│       ├── ChampionsOfKhazad.Bot.Infrastructure/ # Pulumi deployment
│       ├── bot.Dockerfile   # Bot container image
│       └── portal.Dockerfile # Portal container image
└── isleafanofficeryet/      # Static website (Azure Static Web Apps)
    └── index.html
```

### Key Build Commands and Timings
- **NEVER CANCEL**: `dotnet build ChampionsOfKhazad.Bot.slnx` -- 35 seconds, set timeout 120+ seconds
- **NEVER CANCEL**: Frontend `npm ci` -- 15 seconds, set timeout 300+ seconds  
- **NEVER CANCEL**: Frontend `npm run build` -- 15 seconds, set timeout 300+ seconds
- C# formatting check: `dotnet csharpier check .` -- 1 second
- Frontend linting: `npm run lint` -- 2 seconds

### Project Dependencies and Technologies
- **.NET 9**: Main runtime for bot and portal backend
- **Node.js 24+**: Required for frontend build (uses Vite)
- **React 19**: Frontend UI framework
- **TypeScript**: Frontend language
- **Discord.Net**: Discord API library
- **MongoDB**: Database (connection string in appsettings)
- **OpenAI APIs**: AI chat functionality  
- **Azure Services**: Storage, Container Apps deployment
- **Pulumi**: Infrastructure as Code (ChampionsOfKhazad.Bot.Infrastructure project)

### Configuration Files
- **Bot Settings**: `bot/src/ChampionsOfKhazad.Bot/appsettings.json` (production)
- **Dev Settings**: `bot/src/ChampionsOfKhazad.Bot/appsettings.Development.json` (local dev)  
- **Portal Settings**: `bot/src/ChampionsOfKhazad.Bot.Portal/appsettings.json`
- **Frontend Config**: `bot/src/ChampionsOfKhazad.Bot.Portal/frontend/package.json`
- **C# Formatting**: `bot/.csharpierrc.json` (print width: 150)

### Development Limitations
- **Cannot run bot locally**: Requires Discord bot token, MongoDB connection, OpenAI API key, and other production secrets
- **Cannot test full Discord integration**: Bot functionality requires live Discord server
- **Can run and test**: Frontend UI (via dev server), build validation, code formatting, linting
- **Docker builds work**: Both bot and portal Dockerfiles build successfully but need secrets to run

### CI/CD Workflows  
- **bot-build.yml**: Validates .NET build, frontend build, and Pulumi deployment dry-run on PRs
- **bot-deploy.yml**: Deploys to Azure Container Apps on main branch pushes
- **azure-static-web-apps**: Deploys isleafanofficeryet static site

### Common Issues and Solutions
- **C# formatting errors**: Run `dotnet csharpier format .` to auto-fix
- **Frontend build fails**: Ensure Node.js 22+ is installed, run `npm ci` to reinstall dependencies  
- **Missing .NET 9**: Install using the wget command above and export PATH
- **Timeout on builds**: Use adequate timeouts (120s+ for .NET, 300s+ for npm operations)
- **Docker context issues**: Docker commands should be run from `bot/src/` directory

### Validation Scenarios
After making changes, always:
1. **Build validation**: Run the appropriate build command for the area changed
2. **Linting validation**: Run formatting/linting tools to ensure CI passes
3. **Frontend UI validation**: If UI changed, start dev server and verify the change works
4. **Integration validation**: Consider impact on Discord bot functionality (though cannot test locally)

The codebase is production-ready and actively deployed, so prioritize stability and thorough validation of all changes.