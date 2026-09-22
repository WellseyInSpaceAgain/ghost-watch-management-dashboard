# Ghost Watch Management Dashboard

A local Ghost Watch operations console. Economics is the first module; the full brief is in [initial-prompt.md](initial-prompt.md).

## Current milestone

Implemented: dark responsive console, active Track overview, create/edit/archive/restore Economy Tracks, durable SQLite storage, API validation, and revision checks that prevent stale edits from overwriting newer notes. Archived Tracks retain their IDs, notes and creation dates.

The rest of the brief is still pending: EVE SSO/ESI, account grouping, financial workflows, Runs, Capital Pools, knowledge records, Objectives, snapshots and charts. The headline metrics currently show explicit unavailable states. No live character authentication has been attempted.

See [implementation progress](docs/implementation.md) and the [read-only EVE integration review](docs/eve-integration-reference.md).

## Development

Requires .NET SDK 10 and Node.js 22.22.3+ (or a supported newer version).

```bash
dotnet restore
dotnet tool restore
dotnet run --project src/GhostWatch.Api
```

In another terminal:

```bash
cd src/GhostWatch.Web
npm ci
npm start
```

Open http://localhost:4200. The Angular development server proxies `/api` to the backend on http://localhost:5080.

## Verification

```bash
dotnet build
dotnet test
cd src/GhostWatch.Web
npm run build
npx playwright install chromium
npm run test:e2e
```

Browser tests start their own API and web servers on ports 5081 and 4201. They create unique ignored databases under `src/GhostWatch.Api/App_Data/e2e`; they do not use your normal database. On Linux, Chromium also needs its usual browser runtime libraries.

## Structure

- `src/GhostWatch.Api`: .NET 10 API, EF Core 10 / SQLite persistence.
- `src/GhostWatch.Web`: Angular 22 and Angular Material console.
- `tests/GhostWatch.Tests`: backend integration and domain tests.
- `docs`: implementation decisions, progress, and EVE reference notes.

This is a new application, database and Git history. The reference exporter remains read-only. No credentials or runtime data are copied. The existing IDE workspace directory is retained; the product identity is **Ghost Watch Management Dashboard**.

## Local data and migrations

The API applies committed migrations on startup. Default database: `src/GhostWatch.Api/App_Data/ghost-watch.db` when launched with `dotnet run`. Override the directory with `Storage__Directory` (prefer an absolute path). There is no automatic sample-data seeding.

The initial entity is `EconomyTrack`; its migration is `AddEconomyTracks`. Capital Pool and character relationships, selected KPIs, and financial aggregates will be added with their owning features.

```bash
dotnet ef migrations has-pending-model-changes --project src/GhostWatch.Api
```

For a local backup, stop the application and copy its entire data directory. SQLite files and future key rings are ignored by Git and excluded from the Docker build context. Local configuration can go in ignored `src/GhostWatch.Api/appsettings.Local.json`; environment variables take precedence. Do not put EVE secrets in tracked configuration.

## Docker

```bash
docker compose up --build -d
```

Open http://localhost:8080. The multi-stage image serves the compiled Angular UI and API from one origin. SQLite persists in the `ghost-watch-data` volume. The published port binds to loopback for personal/local use; there is no multi-user application authentication.

```bash
docker compose down
```

This preserves the volume. Do not add `--volumes` unless you intend to delete the database. Docker was not available in the initial development environment, so the container recipe has not been executed there.
