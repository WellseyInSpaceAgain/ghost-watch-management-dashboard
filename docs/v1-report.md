# Ghost Watch Management Dashboard — v1 report

## Result and verification boundary

All 45 application acceptance criteria in `initial-prompt.md` are implemented and verified by the automated checks listed in [implementation-status.md](implementation-status.md). The final pass completed with **85 backend tests**, **24 browser tests**, backend and frontend production builds, no pending Entity Framework model changes, and an isolated host Podman Compose startup/migration/persistence test.

The user previously verified the original EVE connection/refresh workflow. A read-only check of the running application found six connected characters with known grants and no missing required scopes; all seven sections supported by that older running build had successful collected data without errors/warnings. This does **not** establish live verification of the new ten-section build.

Two external checks remain blocked:

- Deploying the new build to the existing live container and exercising the added PI/standings/loyalty sections: automatic approval review rejected the proposed stop/backup/update because it would interrupt the service and copy the database plus encryption keys into a repository-local backup. Nothing in that rejected command executed. The running application remains on its earlier build. Explicit approval of the brief interruption and exact private backup location is required before retrying that operation.
- A fresh targeted EVE re-authorisation round trip needs the user's browser consent. Automated signed-token, callback, cancellation, wrong-character and preservation tests pass; no fresh live consent is claimed.

## Architecture and repository structure

A new .NET 10 / ASP.NET Core application serves an Angular 22 / Angular Material console and JSON API. Entity Framework Core 10 persists a new SQLite database. One local Compose service serves the compiled frontend and API from the same origin, with a named volume for the database and data-protection keys. The host port binds to loopback. This is a personal local application, not a multi-user service.

```text
GhostWatch.slnx
src/
  GhostWatch.Api/
    Data/                    DbContext and new application migrations
    Economics/               Tracks, Capital, Runs, Planning, Replacement,
                             Reporting, Snapshots, Charts, FinancialMath
    Eve/Auth/                SSO, tokens, grants and browser-bound state
    Eve/Esi/                 Client, queue, factual refresh and assessments
    Eve/Inventory/           Names, categorisation and inventory projections
    Knowledge/               Playbooks, revisions, Records and links
    Management/              Accounts, assignments and Character/Track links
  GhostWatch.Web/
    src/app/                 Standalone routed operational components
    src/app/charts/          Shared library, renderer and page layout area
    e2e/                     Isolated Playwright workflows
 tests/GhostWatch.Tests/     Backend/domain/integration tests
 scripts/verify-container.py Isolated Compose smoke and persistence check
 docs/                      Setup, contracts, audit, progress and economic plan
 compose.yaml               Supported container operation
 .env.example               Tracked empty configuration template
```

The existing IDE workspace path remains `ghost-watch-management-dashboard`; product/UI/application identity is **Ghost Watch Management Dashboard**, with Economics as the first module. No future operations modules were built.

## Reference reuse

The separate `/home/wellsey/Dev/eve-economic-snapshot-exporter` repository was inspected read-only. Selective adaptations include its SSO metadata/token validation helpers, ESI request/retry/pagination/cache patterns, per-character refresh gating, trained/active capacity formulas, evidence-based economic foundations and exact inventory/blueprint classification rules.

Ghost Watch has its own application identity, database, migrations, domain model, configuration, Angular application and Git history. No exporter database, credentials, key ring, build output, migrations or `.git` directory were imported. [The integration review](eve-integration-reference.md) records inspected files, endpoints and assumptions.

## Persistent entities

| Boundary | Entities |
|---|---|
| EVE identity and facts | `EveCharacter`, `EveSection`, `EveIndustryJob` |
| Lookup caches | `PublicEveLookup`, `EveLocationName` (private names partitioned by character) |
| Manual character planning | `ManagedAccount`, `CharacterPlan`, `CharacterTrack` |
| Economics | `EconomyTrack`, `CapitalPool`, `CapitalAdjustment`, `EconomicRun`, `RunJob` |
| Planning | `Objective`, `TrackStrategy`, `TrackKpiSelection` |
| Knowledge | `Playbook`, `PlaybookRevision`, `EconomicRecord`, `KnowledgeLink` |
| Doctrine/history | `ReplacementPackage`, `EconomicSnapshot` |
| Visualisation | `ChartDefinition`, `ChartPlacement` |

Restrictive foreign keys preserve local relationships. Revision tokens reject stale edits. Factual jobs use stable `(CharacterId, JobId)` identity; missing jobs in a later ESI lookback window remain in history. Snapshot values retain their names and calculated values independently of current records.

### Migrations

- `20260922212404_AddEconomyTracks`
- `20260922213638_AddEveCharacterAuthentication`
- `20260922215018_AddEveFactualData`
- `20260922220916_AddInventoryMetadata`
- `20260922222636_AddCharacterLocationNames`
- `20260922223622_AddCharacterGrantedScopes`
- `20260922225144_AddCharacterManagement`
- `20260922230538_AddCapitalPools`
- `20260922231320_AddEconomicRuns`
- `20260922232350_AddObjectivesAndStrategy`
- `20260922233725_AddKnowledgeDocuments`
- `20260922234243_AddReplacementPackages`
- `20260922234726_AddTrackKpiSelection`
- `20260922235420_AddEconomicSnapshots`
- `20260923000536_AddChartDefinitionsAndPlacements`

## Factual/local separation and authentication

Refresh services write EVE sections, stable factual jobs, lookup caches and validated token/grant metadata. Accounts, assignments, Tracks, Runs, pool assignments/history, knowledge/revisions/links, Objectives, verdicts, finances, strategies, selected KPIs, snapshots and chart definitions/placements are separate tables. The refresh integration test populates all those families and compares fresh database reads after success, partial failure, malformed jobs and ESI history expiry.

EVE single sign-on (SSO) uses browser-bound, one-use state and Proof Key for Code Exchange (PKCE), validates issuer/audiences/signature/expiry and stable character/owner identity, keeps access tokens in memory and persists only protected refresh tokens. The data-protection key ring persists with the application's database. Refresh-token rotation is serialised per character. Authentication HTTP logging is suppressed and callback errors are sanitised.

Granted scopes come from validated token claims. `EveScopes` centrally defines required permissions, operation mappings and missing-scope calculation. Missing permissions skip the affected operation while retaining earlier facts. Targeted re-authorisation requests the full current scope set, rejects a different returned character and preserves the existing local record and management relationships.

### ESI endpoints and scopes

Character endpoints below are relative to `https://esi.evetech.net/characters/{id}/`.

| Data | Endpoint | Scope |
|---|---|---|
| Skills | `skills` | `esi-skills.read_skills.v1` |
| Queue | `skillqueue` | `esi-skills.read_skillqueue.v1` |
| Industry jobs | `industry/jobs?include_completed=true` | `esi-industry.read_character_jobs.v1` |
| Blueprints | `blueprints` (all pages) | `esi-characters.read_blueprints.v1` |
| Assets | `assets` (all pages) | `esi-assets.read_assets.v1` |
| Wallet | `wallet` | `esi-wallet.read_character_wallet.v1` |
| Orders | `orders` | `esi-markets.read_character_orders.v1` |
| Standings | `standings` | `esi-characters.read_standings.v1` |
| Loyalty | `loyalty/points` | `esi-characters.read_loyalty.v1` |
| Planetary Interaction (PI) | `planets` (colony summaries) | `esi-planets.manage_planets.v1` |
| Private structure names | `/universe/structures/{id}` | `esi-universe.read_structures.v1` |

Public universe type/group/entity/location/planet names use unauthenticated lookup endpoints. Private names are scoped and cached per character; metadata failure gives explicit unavailable-name fallbacks. All pages of a factual collection must succeed and validate before replacing it. ESI compatibility is pinned to `2026-09-22`; request caching, bounded retries and shared error-budget cooldown follow the inspected integration.

## Economic workflows and calculations

A Run belongs to one Track, optionally a Playbook and Capital Pool. It can be completely manual or created from an imported industry job. `RunJob` allows several jobs per Run and at most one current Run per job. Removing an association retains the job; ESI never replaces Run annotations, estimates or actuals.

Capital Pools are conceptual allocations, not EVE wallets. Adjustments/transfers append immutable history and update pools atomically. Programme roles identify Core Capital, Treasury, T3 R&D and Expansion Capital independently of names. Allocation totals include archived balances until released. Complete collected wallets support an over-allocation warning; incomplete or stale factual balances remain explicit.

`FinancialMath`, `RunMetrics` and `EconomicReporting` own calculations. Expected and actual values are independent. Null means unknown, not zero. Metrics include profit, margin, slot-days, profit per slot-day, capital turn/time-to-sell, allocated/committed/available capital, 30-day and lifetime Track P/L, recorded research-and-development (R&D) spend, active/completed Runs, replacement coverage and manual internalisation. Tracks select up to six catalogued key performance indicators (KPIs). Default-pool allocation/availability is labelled separately from Track commitments because pools may be shared. [Financial formulas and semantics](financial-metrics.md) list the exact calculations.

Needs Attention checks unassociated jobs; completed commercial Runs missing sales; completed Runs lacking verdicts; pool commitments at least 90% of allocation; incomplete active checklists; overdue active objectives; over-allocation; and missing/stale wallets. R&D success is independent of revenue/profit. The rules produce links, never automatic actions or scores.

## Snapshots

**Take Snapshot** accepts an optional name/note and stores a versioned JSON capture of programme finance, factual completeness, allocations/commitments, Track metrics and selected KPIs, replacement estimates/coverage and Objective/Gate state. History reads saved values without recalculating them.

A lightweight background worker checks at startup and hourly. A transaction and unique nullable `yyyy-MM` key permit one automatic snapshot per UTC month; manual snapshots have no month key. Starting later in the month captures that current state. Missing historical months are not fabricated. There are no snapshot update/delete endpoints.

## Chart model, schema and layout

`ChartDefinition` stores name/title/description/configuration and revisions. `ChartPlacement` independently stores its shared definition reference, Dashboard/Track page context, sort order and Small/Medium/Wide width. Removing a placement does not delete a definition. Editing a shared definition affects every placement.

The typed `ChartConfig`, `ChartFilters`, `ChartAxis` and `ChartSeries` contracts reject unknown properties and unsupported data. Source fields and simple filters are explicitly catalogued. Sources are Runs, Tracks, Capital Pools, stored Snapshots and Objectives. Aggregations are sum/average/min/max/count/latest. One Chart.js renderer supports line/bar/stacked bar/pie/donut/KPI and accessible data tables. [The full contract](chart-configuration.md) and [JSON schema](chart-config.schema.json) document limits, dates, unknowns, snapshot scope and the sample.

```json
{
  "title": "Core Capital over time",
  "description": "Stored monthly state, without reconstructing historical wallets",
  "type": "line",
  "dataSource": "snapshots",
  "x": { "field": "month", "label": "UTC month" },
  "series": [
    { "field": "coreCapital", "label": "Core Capital", "aggregation": "latest", "format": "isk" }
  ],
  "timeRange": "all"
}
```

A new editor starts with an expected/actual Run example and validates a debounced live preview. `CURRENT_TRACK` binds to placement/preview context, never silently to all Tracks. Drag/drop uses Angular Component Dev Kit (CDK), with Move up/down alternatives. **Save layout** validates the exact placement set and revisions, then persists order/width atomically. Canvas resizing is responsive. Chart dependencies load separately from fixed operational page content.

## Tests and audit evidence

- Backend: 85 passing tests covering auth/grants/wrong-character callbacks, token protection, scope-aware retained refresh, pagination/retries, trained/active assessment, inventory/name projections, accounts/links, pool adjustments/wallet isolation, Run creation/association/financials, planning, knowledge revisions, coverage, Track aggregates/KPIs, all deterministic attention rules, immutable/monthly snapshots and validated/shared charts.
- Browser: 24 passing workflows covering character permissions/progress, named data/inventory, accounts/assignments, Tracks/archive/restore, capital/history, manual/assisted Runs, Gates/stages, Markdown/sanitization/revisions, replacements, reporting, snapshots, every renderer and real pointer drag with width/order reload persistence and cross-page shared edits.
- Backend build: zero warnings/errors. Angular production build passes. No pending EF model changes.
- Isolated Compose: built final application image, verified non-root runtime, health/API/frontend deep links, automatic migrations and named-volume persistence across recreation. Only its own temporary volume was removed.
- Source audit: no production TODO/stub behaviour found. Remaining `placeholder` matches are input hints, Angular deferred-loading placeholders or drag placeholders. Authentication “pending” matches are legitimate state handling.
- Tracked-file audit: no environment secrets, authentication databases, key rings, node_modules, build output or private runtime directories are tracked. `.env.example` is tracked; real `.env` and runtime data remain ignored. No credentials were read for this final audit.

## Deliberate limits

No live pricing/scanning, static-data-export production trees, recipe eligibility claims, automated trading/purchasing/skill planning, inventory reservations, external killboards/Discord/corporation APIs, automatic reimbursements, accounting ledger, AI advice, arbitrary SQL/JavaScript, freeform dashboard canvas or workflow engine. PI is colony-summary level. Wallet transactions/journal and raw job/order chart sources are outside the chosen v1 data-source set. Historical financial values only begin when snapshots are captured. Development fixtures stay in isolated tests; normal databases receive no sample management records or forced charts.

## Build, run and local Git identity

```bash
dotnet restore
dotnet tool restore
dotnet build
dotnet test
dotnet ef migrations has-pending-model-changes --project src/GhostWatch.Api
npm ci --prefix src/GhostWatch.Web
npm run build --prefix src/GhostWatch.Web
# Browser verification, from src/GhostWatch.Web:
npm run test:e2e
```

For development, run `dotnet run --project src/GhostWatch.Api` and `npm start` from `src/GhostWatch.Web`, then open http://localhost:4200. For containers, copy `.env.example` to ignored `.env`, configure SSO locally and use `docker compose up -d --build` or `podman compose up -d --build`. On this Distrobox use `distrobox-host-exec podman compose up -d --build`. Open http://localhost:8080. Preserve the named volume and its keys. See [container operation](containers.md) for shutdown and isolated smoke testing.

Verified repository-local identity:

```text
WellseyInSpaceAgain
wells3y@gmail.com
```

Only repository-local Git configuration was set during implementation; global Git configuration was not changed. The repository was initialised independently and has incremental feature commits. The following history was captured at the final implementation checkpoint, before the audit/documentation commit; `git log --oneline --decorate` shows the latest complete history.

```text
6a9c21c (HEAD -> main) Add validated reusable charts and persistent page layouts
056b1bc Preserve economic history with manual and monthly snapshots
2326e49 Connect operational Track reporting and programme dashboard metrics
1d1b0ae Add Replacement Packages and Treasury coverage
4031c8d Add Markdown Playbooks revisions and flexible linked Records
608271d add v1 economic plan to docs
9ac8f2f Add Objectives Gates and manual production-stage strategy
0b4a14e Add economic Runs and persistent industry-job associations
f68c332 Add conceptual Capital Pools and allocation history
768b3cb Complete economic EVE data views and character foundations
e577afa Add account grouping and character economic assignments
25cd56b Audit v1 acceptance criteria and record autonomous implementation plan
61d6b79 Add per-character ESI permissions and targeted re-authorisation
0398c7d Show character refresh progress and resolve displayed EVE names
014457e Add refresh all characters action
144ec44 Add searchable asset and blueprint inventory views
37f272a Add paginated assets and blueprints with public metadata caching
33dd2a2 Add queued ESI character refresh and retained factual history
fd669e1 Use Compose for container workflows and add environment template
88865ff Add EVE SSO character authentication and protected token storage
e67e0c4 Verify host Podman deployment from Distrobox
5c38639 Document EVE integration references and local deployment
fdd85f5 Add persistent Economy Track management
e29f28e Initialise Ghost Watch Management Dashboard
```
