# Repository Guidelines

## Project Structure & Module Organization

`bot/` contains the .NET solution (`ChampionsOfKhazad.Bot.slnx`). Under `bot/src/`, `ChampionsOfKhazad.Bot/` is the Discord host; feature projects isolate core, lore, memes, AI, MongoDB, and Raid Helper code. `ChampionsOfKhazad.Bot.Portal/` contains the ASP.NET portal and its feature-organized React client in `frontend/src/`. Pulumi code is in `ChampionsOfKhazad.Bot.Infrastructure/`. `isleafanofficeryet/` is an independent static site; `.github/workflows/` defines CI and deployment.

## Build, Test, and Development Commands

Use .NET 10 and Node.js 24. Run backend commands from `bot/`:

- `dotnet tool restore` installs the pinned CSharpier formatter.
- `dotnet build ChampionsOfKhazad.Bot.slnx` restores and builds all .NET projects.
- `dotnet test --solution ChampionsOfKhazad.Bot.slnx --no-build --minimum-expected-tests 1` runs the backend tests after building.
- `dotnet csharpier check .` verifies C# formatting; use `dotnet csharpier format .` to fix it.

Run frontend commands from `bot/src/ChampionsOfKhazad.Bot.Portal/frontend/`:

- `npm ci` installs the lockfile-defined dependencies.
- `npm run dev` starts Vite at `http://localhost:5173`.
- `npm run build` type-checks and produces the production bundle.
- `npm test` runs the frontend request and action tests with Node's built-in test runner.
- `npm run lint` runs ESLint and Prettier checks.

The full bot requires Discord, MongoDB, OpenAI, Auth0, and Azure configuration; builds do not.

## Documentation

Keep README files focused on operational information: prerequisites, configuration, build/test commands, running, and deployment. Document feature behavior, policies, design decisions, and durable engineering knowledge in nearby `docs/` pages instead. Read relevant feature documentation before changing behavior and update it when those constraints change.

- `bot/docs/notebook.md` — temporary notebook selection, evidence/review requirements, privacy, and diagnostic semantics.
- `bot/docs/background-notebook-observer.md` — background observation, shared channel scope, event scheduling, budgets, and checkpoint semantics.

## Coding Style & Naming Conventions

C# is formatted by CSharpier with a 150-column width. Preserve nullable reference types and implicit usings. Use four-space indentation, PascalCase for types and members, `I`-prefixed interfaces, and one primary type per matching file. Keep domain code in its existing feature project. TypeScript uses Prettier and ESLint, two-space indentation, PascalCase React components, and camelCase functions/hooks. Follow existing names such as `GeneratedImages.page.tsx` and `use-generated-images.ts`.

## Testing Guidelines

Backend tests use xUnit in clearly named `*.Tests` projects included in the solution. Frontend request and action tests use Node's built-in test runner in `frontend/tests/`. There is no coverage threshold. Validate every change with the relevant tests, build, and lint commands. For UI work, also exercise the changed flow with `npm run dev`. Add regression tests for behavior changes, and include new backend test projects in the solution.

## Commit & Publishing Guidelines

This repository does not use feature branches or pull requests. Work, commit, and push directly on the current branch only when the user asks to publish. History favors short, imperative summaries without scope prefixes, for example `add week check event` or `Update AI model constants`. Keep commits focused; reserve `Bump ...` wording for dependency updates. Ensure all applicable GitHub Actions checks pass.

## Security & Configuration

Never commit tokens, connection strings, or populated local settings. Supply secrets through environment variables, .NET user secrets, or the deployment platform, and keep committed `appsettings*.json` files free of credentials.
