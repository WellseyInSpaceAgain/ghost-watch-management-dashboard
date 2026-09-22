# Ghost Watch Management Dashboard

A local Ghost Watch operations console. Economics is the first module; the full brief is in [initial-prompt.md](initial-prompt.md).

## Current milestone

Implemented: dark responsive console, active Track overview, create/edit/archive/restore Economy Tracks, durable SQLite storage, API validation, revision checks that prevent stale edits from overwriting newer notes, and EVE SSO character connections with protected refresh-token storage. Archived Tracks retain their IDs, notes and creation dates.

The rest of the brief is still pending: ESI data refresh, account grouping, financial workflows, Runs, Capital Pools, knowledge records, Objectives, snapshots and charts. The headline metrics currently show explicit unavailable states. Live EVE authentication remains pending your separate SSO registration; automated tests use a fake EVE server.

Start with [EVE SSO setup](docs/eve-sso-setup.md) to register the application and connect characters. For host Podman from Distrobox, see [container instructions](docs/containers.md).

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

The current entities are `EconomyTrack` and `EveCharacter`, in separate local-management and EVE identity tables. Migrations: `AddEconomyTracks` and `AddEveCharacterAuthentication`. Capital Pool and character relationships, selected KPIs, and financial aggregates will be added with their owning features.

```bash
dotnet ef migrations has-pending-model-changes --project src/GhostWatch.Api
```

For a local backup, stop the application and copy its entire data directory. SQLite files and future key rings are ignored by Git and excluded from the Docker build context. Local configuration can go in ignored `src/GhostWatch.Api/appsettings.Local.json`; environment variables take precedence. Do not put EVE secrets in tracked configuration.

## Containers

```bash
cp .env.example .env
chmod 600 .env
```

Edit `.env` locally to configure EVE SSO. Empty credentials allow the app to run with the setup prompt.

```bash
docker compose up -d --build
# or: podman compose up -d --build
# From this Distrobox: distrobox-host-exec podman compose up -d --build
```

Open http://localhost:8080. The multi-stage image serves the compiled Angular UI and API from one origin. SQLite persists in the `ghost-watch-data` volume. The published port binds to loopback for personal/local use; there is no multi-user application authentication.

```bash
docker compose down
# or: podman compose down
```

This preserves the volume. Do not add `--volumes` unless you intend to delete the database. The image has been built and smoke-tested using host Podman through `distrobox-host-exec`. Container startup and smoke verification use the repository Compose file.

To repeat host Podman image and persistence verification from Distrobox:

```bash
python3 scripts/verify-container.py
```

The smoke test creates and removes only its own temporary container and data volume.
