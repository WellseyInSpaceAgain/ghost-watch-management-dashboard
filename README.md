# Ghost Watch Management Dashboard

A local Ghost Watch operations console. Economics is the first module; the full brief is in [initial-prompt.md](initial-prompt.md).

## Current milestone

Implemented: dark responsive console, active Track overview, create/edit/archive/restore Economy Tracks, durable SQLite storage, API validation, revision checks that prevent stale edits from overwriting newer notes, EVE SSO character connections with protected refresh-token storage, and queued ESI refresh for wallets, skills, skill queues, market orders, industry jobs, assets and blueprints. Archived Tracks retain their IDs, notes and creation dates.

The rest of the brief is still pending: PI/standings/LP refresh, account grouping, financial workflows, Runs, Capital Pools, knowledge records, Objectives, snapshots and charts. The headline metrics currently show explicit unavailable states. The user has manually verified the character connection/refresh workflow. The new assets/blueprints feature is covered by simulated EVE tests; live inventory verification remains to be done.

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

The current entities are `EconomyTrack`, `EveCharacter`, `EveSection`, `EveIndustryJob` and `PublicEveLookup`, with local-management tables separate from EVE identity/facts. Migrations: `AddEconomyTracks`, `AddEveCharacterAuthentication`, `AddEveFactualData` and `AddInventoryMetadata`. Capital Pool and character relationships, selected KPIs, and financial aggregates will be added with their owning features.

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

## Character refresh

Open **Characters**, select a connected character, then choose **Refresh EVE data**. Refresh runs in a background queue; the page polls only while queued/running. Repeated requests for the same character are rejected while its refresh is pending. The Characters page also has **Refresh all characters**, which queues every connected character and reports already-running refreshes or individual failures.

Wallet balance, trained/active skill capacity and industry jobs have dedicated views. Raw collected records expose skills, queue and market orders for inspection; name enrichment and richer record views follow later. Each section shows attempt/success timestamps and safe errors. Failed responses retain previous data; jobs retain stable identity and history when absent from later ESI responses. No refresh writes to Economy Tracks.

The character workflow has been manually verified by the user. This does not imply that the new inventory feature has been live-verified. The automated refresh tests use simulated EVE responses, including two-character refresh, failures, retries and malformed payloads.

## Assets and blueprints

After updating the application with Compose, open a connected character and refresh. No additional scopes are requested beyond the original asset/blueprint scopes. The first inventory refresh may take longer while public type and group metadata is collected; it is cached for 30 days across characters.

The Assets panel offers searchable stock summaries and individual item locations, availability/category filters, and 25-row pagination. Loose hangar stock is separated from fitted/contained assets; ambiguous locations remain unknown. Blueprint tables show originals/copies, stack quantity, material/time efficiency and copy runs remaining. Original runs display as Unlimited, and a copy with zero runs remains zero.

All ESI pages must succeed and validate before the previous collection is replaced. A failed page retains the previous complete data and timestamp. Metadata failures instead save the complete raw inventory with a separate warning, using known names or explicit type-ID/unknown-category fallbacks. Location names, structure access and inventory reservation are not implemented; locations are shown as IDs and flags.
