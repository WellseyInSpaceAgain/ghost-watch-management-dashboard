# Ghost Watch Management Dashboard

A local Ghost Watch operations console. Economics is the first module; the full brief is in [initial-prompt.md](initial-prompt.md).

## Economics v1

Manage Economy Tracks, manual or ESI-assisted Runs, conceptual Capital Pools, Objectives/Gates, Markdown Playbooks with revision history, flexible Records, Replacement Packages and immutable Economic Snapshots. Dashboard and Track pages combine operational tables, selected financial metrics, deterministic Needs Attention and reusable charts with persistent drag ordering and widths.

Connect multiple EVE characters, group them into accounts, record subscription/assignments and refresh ten factual sections. Skills/capacity distinguish trained from currently usable levels. Named inventory distinguishes available stock, contained/fitted items, originals and blueprint copies. Per-character permissions and targeted re-authorisation preserve local management data.

Start with [EVE SSO setup](docs/eve-sso-setup.md). For host Podman from Distrobox, see [container instructions](docs/containers.md). The [v1 status](docs/implementation-status.md), [implementation log](docs/implementation-log.md), [financial formulas](docs/financial-metrics.md), [chart schema and examples](docs/chart-configuration.md) and [EVE integration reference](docs/eve-integration-reference.md) describe the implementation. The [economic plan](docs/v1-economic-plan.md) records the user's operating strategy; it does not seed or overwrite application data.

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

Local management entities are separate from EVE identity, raw factual sections and public/private lookup caches. Committed migrations cover Tracks, character authentication/facts/scopes, accounts/assignments, capital/history, Runs/job links, Objectives/strategy, knowledge/revisions, Replacement Packages, selected KPIs, snapshots and charts. See [the v1 report](docs/v1-report.md) for the complete entity and migration inventory.

```bash
dotnet ef migrations has-pending-model-changes --project src/GhostWatch.Api
```

For a local backup, stop the application and copy its entire data directory. SQLite files and persistent data-protection key rings are ignored by Git and excluded from the Docker build context. Local configuration can go in ignored `src/GhostWatch.Api/appsettings.Local.json`; environment variables take precedence. Do not put EVE secrets in tracked configuration.

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

Open **Characters**, select a connected character, then choose **Refresh EVE data**. Refresh runs in a background queue; the page polls only while queued/running. Repeated requests for the same character are rejected while its refresh is pending. The Characters page also has **Refresh all characters**, which queues every connected character and reports already-running refreshes or individual failures. Each character shows a live spinner, current section and x/n stage position, followed by Updated, Needs attention or Failed. The count denotes stage position, not successful section count; progress is in memory and resets to Idle on an application restart.

Wallet balance, trained/active capacity, skills, queue, orders, jobs, assets, blueprints, standings, loyalty points and PI colonies have named factual views. Each section shows attempt/success timestamps and safe errors. Failed responses retain previous data; jobs retain stable identity and history when absent from later ESI responses. Refresh only updates EVE identity/token metadata, facts and lookup caches; it never updates local economic management records.

The user manually verified the original character workflow. Current live-check results and any remaining external verification are recorded in the v1 status/report. Automated refresh tests use simulated EVE responses, including multiple characters, failures, retries and malformed payloads.

## Assets and blueprints

After updating the application with Compose, open a connected character and refresh. For private structure names, add `esi-universe.read_structures.v1` to your EVE application registration and reconnect each character; see [SSO setup](docs/eve-sso-setup.md#location-names-after-upgrading). The first inventory refresh may take longer while public type and group metadata is collected; it is cached for 30 days across characters.

The Assets panel offers searchable stock summaries and individual item locations, availability/category filters, and 25-row pagination. Loose hangar stock is separated from fitted/contained assets; ambiguous locations remain unknown. Blueprint tables show originals/copies, stack quantity, material/time efficiency and copy runs remaining. Original runs display as Unlimited, and a copy with zero runs remains zero.

All ESI pages must succeed and validate before the previous collection is replaced. A failed page retains the previous complete data and timestamp. Metadata failures instead save the complete raw inventory with a separate warning, using known names or explicit type-ID/unknown-category fallbacks. Locations show station/system names, accessible structure names, or container type and parent location. IDs and flags remain secondary references, with explicit fallbacks for unavailable names. Industry jobs display product/blueprint names. Structure names are cached per character; a resolved name does not establish inventory access or reserve stock.

### ESI permissions and re-authorisation

The Characters list shows a compact permission status; each character's **ESI Permissions** panel lists missing scopes and offers **Re-authorise Character**. Enable the scopes in your EVE application registration, then use that action and select the same character. Successful re-authorisation returns to its detail page, updates permissions and retains the character and local data. Selecting another character is rejected.

Existing connections initially show **Permissions not checked** after this upgrade. Refresh EVE data to establish grants from a verified access token, or re-authorise. Missing permissions skip only the affected refresh operations and preserve previous facts. Adding a new application scope automatically updates these warnings; token refresh does not automatically grant new permissions.

### Accounts and economic character data

Use **Accounts** to create manual account groups and record Unknown/Alpha/Omega subscription state. On character detail, **Economic assignment** saves account membership, a planning label, notes and any number of Track links. These records remain separate from EVE identity and refreshable facts.

Character detail provides searchable named skills, skill queue, orders, PI colonies, standings and loyalty tables. Economic foundations show trained and active skill evidence, dormant skills and explicit limits on recipe eligibility. Refresh uses ten independently retained sections; metadata failures preserve raw facts with warnings.

### Capital and execution

**Capital Pools** manages conceptual allocations, targets and adjustment/transfer history, with warnings when complete collected wallets do not cover the allocation. Set a Track's default pool in its details. Programme roles identify Core Capital and Treasury independently of pool names.

**Runs** supports manual batches, trading, PI, R&D, strategic supply and other attempts. Keep expected and actual input/other cost/revenue separate. Blank amounts remain unknown; explicit zero is valid. Completed/evaluated Runs require a completion date but do not require revenue.

**Industry Jobs** lists retained factual jobs with their current Run/Track association. Create a Run from a job, associate several jobs with an existing Run, or remove an association without deleting the job. ESI refresh preserves all local annotations. Active/Selling Runs commit complete actual costs or, when unavailable, complete expected costs; a missing commitment estimate makes available capital unknown.

## Knowledge, planning and history

**Playbooks** are editable Markdown procedures with saved earlier versions. **Records** accept custom types and links to Tracks, Runs, Characters, Playbooks, Objectives and pools. **Objectives / Gates** use manual progress and checklist conditions. Track production stages provide a manual internalisation percentage.

**Replacement Packages** store manual estimates and one optional default; coverage uses the Ghost Watch Treasury allocation. **Snapshots** provides **Take Snapshot** with an optional name/note. A background worker captures the current UTC month on startup when absent and checks hourly thereafter. History stores calculated values independently of later edits; no past months are fabricated.

**Chart library** provides sample JSON, validation and live preview. Add a shared definition to Dashboard or Track chart areas, choose **Edit chart layout**, drag or use Move up/down, select widths and **Save layout**. Removing a placement retains its definition. Definitions update all their placements. [Chart configuration documentation](docs/chart-configuration.md) lists supported fields and historical-value semantics.

Unknown financial inputs remain unknown. Enter zero explicitly when known; a successful R&D verdict does not imply profitable sales. No automatic sample management data is inserted into your normal database.

## License

Licensed under the [MIT License](LICENSE).
