# Implementation log

## Autonomous continuation baseline

- Reviewed the original specification, clean working tree and incremental history through `61d6b79`.
- Existing usable workflows: Track metadata/archive management; EVE authentication and protected tokens; queued partial-safe ESI refresh; named assets/blueprints/jobs; character refresh progress; verified scope awareness and targeted re-authorisation.
- Latest verification: 64 backend tests, 12 browser tests, frontend production build and isolated Compose startup/migrations/persistence check.
- Created `implementation-status.md` against all 45 acceptance criteria. Most management workflows remain unfinished; no scaffolding is counted as completion.
- Continue feature-sized vertical slices, build/test/fix, update these docs, commit and immediately proceed. Final completion requires an audit against the full original prompt. External live checks must not block independent work.
- Account grouping, manual subscription state and character economic/Track assignments are next. Reference account/character controllers were inspected read-only; local management will use separate tables rather than adding mutable planning fields to factual EVE identity.

## Accounts and character planning

- Added separate `ManagedAccount`, `CharacterPlan` and `CharacterTrack` tables with restrictive foreign keys and revision checks. EVE identity/tokens remain in their original table.
- Account creation/editing includes explicit Unknown/Alpha/Omega and notes. Character detail supports account selection, user-defined economic assignment, notes and multiple Track links. Character lists display names/subscription/assignment; Track details link back to assigned characters.
- Reference account/character controllers informed semantics; no credential/account inference, subscription API claims or role derivation from skills.
- Backend tests cover persistence, multi-Track removal, invalid references, stale revisions and credential preservation. Re-authorisation and refresh tests now include the new local management records.
- No hard-delete of account groups: keeping identities avoids accidental loss of assignments. Groups can be renamed and characters can be unassigned.

## Remaining EVE factual views and economic foundations

- Added scoped standings, loyalty-point and PI colony-summary refresh; total refresh stages now come from ten section definitions. No new OAuth scopes beyond the central list were needed.
- Named/searchable/paginated skills, queue, market orders, standings, loyalty and PI views are connected to enriched API projections. Buy commitments and listed sell values remain distinct from realised revenue.
- Selectively adapted the reference economic assessor: trained/active evidence, dormant skills, broad manufacturing/research/reaction/trading/PI/refining/hauling/invention foundations, user-set subscription context and explicit recipe-eligibility unknowns.
- Raw factual JSON remains unmodified; public names and type/group metadata are independent caches. Scope checks and failure retention apply to all added sections.
- Integration pass: backend build and 67 tests pass; Angular production build and all 14 browser tests pass. Existing account, scope and Track workflows remain connected. New live EVE sections remain separately blocked on external verification.

## Conceptual Capital Pools

- Added pool creation/editing, target capital, archive/restore and optional programme roles. Core Capital/Treasury/etc. roles avoid identifying financial concepts by editable display names; each named role has at most one active pool.
- Allocations change only through immutable adjustment history. Transfers update both pools and append history in one EF save transaction, with revision-based concurrency protection. They never write EVE wallets.
- Over-allocation is allowed and warned about when all connected characters have collected wallets. Incomplete wallets return unknown totals/comparison plus known balance counts; stale balances are labelled. Archived allocations continue to count until explicitly released.
- Track default Capital Pool selection is saved independently from factual data. Run-derived commitments/spend/revenue follow with the next Run slice; they are not fabricated from EVE wallet totals.
- Added central nullable financial functions for subsequent Run/snapshot use. Blank financial values stay unknown; users can explicitly record zero where it is known.
- Backend verification: 69 tests pass, including transfer history/atomicity, over-allocation, wallet isolation, stale revisions and legacy migrations.
- Browser checks verify pool creation, allocation/transfer history and reload persistence; Track archive/restore and narrow-screen checks pass after making the expanded navigation wrap. Angular production build passes.

## Economic Runs and ESI job associations

- Added first-class manual Runs with Track/pool references, type/purpose/status, nullable expected and actual finance, dates, duration, slots, time-to-sell, verdict and notes. Stale edits conflict rather than overwrite. Playbook linkage follows in the knowledge slice.
- Added `RunJob` with stable `(CharacterId, JobId)` identity and restrictive foreign keys. One Run may hold several jobs; a job belongs to at most one Run until explicitly unassociated. Creating an assisted Run and its association is atomic.
- Industry Jobs now provides names, association state, Create Run, associate existing Run and remove association. ESI retains ownership of job state; it never changes Run notes or financial values.
- Central financials distinguish estimates from actuals and preserve unknowns. Slot-days require explicit duration and concurrency. R&D can complete with absent revenue and a successful verdict; explicitly recording zero revenue permits calculating a loss.
- Active/Selling Run costs are committed capital: use complete actual costs, otherwise complete expected costs, otherwise unknown. Pool available capital follows allocated minus commitments; immutable conceptual allocations do not automatically absorb trading profit or expenditure.
- Verification: 71 backend tests; full browser integration covered all existing workflows, with a new-Run empty-ID submission bug found and fixed. Both Run workflow tests then pass. Production backend/frontend builds and isolated Compose migrations/persistence checks pass.

## Objectives, Gates and production strategy

- Added Objectives/Gates with optional Track link, status, target date, nullable manual progress, checklist conditions and notes. Completion timestamps are set on completion; reopening clears current completion status without replacing the record. No expression/rules engine or automatic action is introduced.
- Track detail now edits user-defined production stages and internal/external state. The backend calculates internalisation as internal/selected stages; an empty set stays unknown. This never claims recipe capability.
- Revision checks reject stale planning updates; validation covers progress bounds, references and duplicate stage names.
- Backend verification: 73 tests pass; frontend production build passes. Browser coverage exercises a Gate checklist and persisted 50% internalisation.

## Playbooks and flexible Records

- Added Markdown procedures, status/tags, Track/Character/Run links and prior-version Markdown/timestamp retention. Runs now select a Playbook. Records allow user-defined types and links to Tracks, Runs, Characters, Playbooks, Objectives and Capital Pools.
- All references are validated and backed by restrictive foreign keys. Document updates protect against stale revisions; earlier Playbook content remains accessible.
- Markdown uses [Marked](https://github.com/markedjs/marked/blob/master/README.md) followed by Angular's HTML binding sanitizer, never a trust bypass. Browser verification covers hostile embedded HTML as well as editing, revision viewing and linked Record persistence.
- Fixed a zoneless Angular change-detection issue found by the browser test when opening a saved document. Backend suite: 74 passed; knowledge and Run browser workflows pass; production frontend build passes.

## Replacement Packages

- Added manual doctrine estimates and notes with one optional programme default. Switching defaults updates both records in a transaction; stale editors cannot overwrite the switch.
- Coverage uses the active Ghost Watch Treasury pool's conceptual allocation. Missing treasury yields unknown, recorded zero treasury yields zero coverage, and nonpositive package values are rejected. No reimbursement or pricing integration.
- Verification: 75 backend tests, production frontend build and package create/edit/reload browser check pass.
- User added `v1-economic-plan.md` as operating context. The original application specification remains the implementation acceptance baseline; the plan is retained as user-authored documentation.

## Operational reporting and integration pass

- Replaced dashboard placeholder metrics with recorded Core Capital, 30-day realised profit, Treasury and replacement coverage. Added financial Track rows, collected wallet/order state, deterministic Needs Attention and recent activity.
- Track detail now connects selected KPIs, capital/performance, Runs, relevant industry jobs, Objectives/Gates and linked knowledge. Existing character assignments, metadata and manual strategy remain editable. New Run/Objective links prefill Track context; named pool links select the intended editor.
- Central reporting preserves incomplete financial values. Default-pool allocation/availability are explicitly labelled because multiple Tracks may share a pool; programme totals sum pools directly. Detailed formulas and rule definitions are in `financial-metrics.md`.
- Verification: 76 backend tests, production frontend build and all 21 browser tests pass, including navigation, narrow-screen layout, character refresh/scope handling and new operational workflows.
- Isolated host Podman Compose build/start/deep-link/migration/persistence smoke test passes. Only the temporary smoke-test volume was removed; the user's runtime data and credentials were not inspected or changed.

## Immutable Economic Snapshots

- Added manual named/noted captures and automatic UTC monthly captures. A lightweight hosted service checks on startup and hourly; a transactional capture and unique month key prevent duplicate automatic entries. No fabricated historical backfill.
- Snapshots store versioned calculated values and names, not live references for recalculation. The history UI shows programme finance, factual completeness, Capital Pools, Track KPIs and Objective/Gate progress.
- Tests verify persisted history remains unchanged after allocations change, same-month idempotence, next-month creation and actual hosted-worker startup. Automated tests use isolated databases and disable the worker unless testing it explicitly.
- Verification: 78 backend tests pass, frontend production build passes and manual capture/reload/immutability browser workflow passes. Removed an unused UI import reported during the build.

## Reusable chart definitions and placements

- Added strict typed JSON validation, explicit source/field/filter/aggregation catalogs, bounded preview queries and a documented schema/example. Five sources cover Runs, Tracks, Capital Pools, stored Snapshots and Objectives; no arbitrary database queries or code are evaluated.
- Added a debounced preview/editor, shared definition library and one Chart.js renderer supporting line, bar, stacked bar, pie/donut and KPI. Accessible data tables preserve precise values and unknowns. Historical charts read stored snapshot values; unknown inputs propagate through aggregates.
- Dashboard and Track areas reuse definitions through independent placements. CDK drag/drop and accessible ordering controls persist order and Small/Medium/Wide widths in a transaction; revisions detect stale layout edits. Placement removal retains the definition; in-use definitions cannot be deleted.
- Chart dependencies load separately from fixed page content. Browser verification covers actual pointer dragging, reload persistence, shared edits across pages, placement removal, renderer variants and narrow-screen layout after the canvas resize settles.
- Verification: 84 backend tests pass, production frontend build passes, chart browser workflows pass. The earlier approval-service credential failure was resolved after the user signed back in; no denied command was executed.

## Final v1 audit

- Audited the original specification, all 45 acceptance criteria, the scope-awareness request and subsequent Compose/environment/name/progress requirements. All independent workflows are implemented; no scaffold is counted as finished.
- Expanded the ESI boundary test to populate and compare every local management family from fresh database reads across success, partial failures, malformed data and disappearing factual jobs. Added coverage for every implemented attention rule and explicit malformed/unsupported chart configurations.
- Corrected Markdown descendant styling so sanitized generated code blocks, images and tables receive responsive styles. Replaced stale README/reference milestones with current functionality and added the complete `v1-report.md` covering the requested architecture/entity/migration/security/financial/chart/test/history topics.
- Final verification: backend build succeeds with zero warnings/errors; all 85 backend tests pass; frontend production build and all 24 browser workflows pass; no pending EF model changes; isolated Compose startup/migration/non-root/deep-link/persistence check passes. Source scan found no production stubs; tracked-file review found no private runtime/credential artifacts.
- Read-only inspection of the existing live container found six connected, fully scoped characters and complete successful data for its seven older-build sections. No names, balances, token values or raw private payloads were printed.
- A proposed live Compose upgrade was rejected by automatic approval review because stopping the service and copying its database plus data-protection keys into a repository-local private backup lacked explicit approval of the side effects/location. The command did not execute. All unaffected work is complete; the live update and new-section check await that approval. Fresh targeted re-authorisation also remains user-interactive.

## Economic Plan v1 fidelity follow-up — Run accounting and capital efficiency

- Inspected the plan, financial semantics, Run/pool/reporting/chart/KPI/snapshot paths and existing verification. The two gaps are explicit expected/actual Job Cost and Capital Tied Up, and centrally calculated capital efficiency.
- New nullable Run inputs preserve unknowns; Other Cost remains separate. Historical inputs will stay null with no inferred backfill. Total cost includes Job Cost throughout the central calculations, including commitments. Capital Tied Up never substitutes for commitment.
- Track aggregation follows the realised Run cohort: sum(profit) / sum(slot-days × explicit capital tied up), a capital-exposure-weighted ratio. Empty/incomplete/invalid cohorts remain unknown.
- New snapshots use version 2 to identify changed cost semantics and the added Track metric. Version 1 stored JSON remains read directly, with no recalculation or rewriting.
- Implementation and verification in progress.
- Implemented `ExpectedJobCost`, `ActualJobCost` and `CapitalTiedUp` as nullable decimal Run properties, included in existing entity-backed create/update/read payloads and nonnegative financial validation. EF's existing nullable-decimal mapping applies; migration `20260923172146_AddRunJobCostAndCapitalEfficiency` adds only three nullable TEXT columns, without defaults, data updates or snapshot changes.
- Updated central expected/actual costs and profits. Reviewed every `FinancialMath.Cost`/`Profit` call site and all `RunMetrics` consumers: pool commitments/availability/lifetime spend and profit, realised and 30-day reporting, R&D spend, Track operations, charts and new snapshot captures inherit the revised totals. Other Cost remains independent. Missing historical Job Cost makes current totals unknown until recorded; no automatic zero was introduced.
- Added central Run capital efficiency and weighted Track aggregation to the existing reporting/KPI catalog. Track chart and Track-scoped snapshot catalogs inherit the new measure. Capital normalization avoids decimal overflow from multiplying large valid capital and slot-day inputs; it preserves the documented weighted formula.
- Run forms expose both Job Costs and Capital Tied Up with blank/zero semantics and explanatory copy. Saved results expose expected/actual total costs and the capital-efficiency ratio. Angular only formats backend values. Added the `ratio` chart format and shared significant-digit presentation for Run, Track KPI, snapshot and chart displays; existing formats/definitions/layouts retain their meanings.
- Version-1 snapshot tests use a literal historical payload, verify unchanged JSON through migration/API reads, preserve the original month's automatic snapshot/version, and confirm an absent historical efficiency key yields unknown in charts. New captures are version 2 and persist calculated Track efficiency and KPI selection without changing earlier captures.
- Validation testing exposed malformed numeric JSON being turned into HTTP 500 by the existing exception handler. Preserved `BadHttpRequestException` status codes so invalid financial inputs return HTTP 400. Negative, out-of-range and nonnumeric new inputs are rejected without updates.
- Updated existing complete financial fixtures to explicitly record zero Job Cost. Added unit/integration coverage for separate costs/profits, null inputs and invalid denominators, explicit zero/loss ratios, capital independence, create/update/clear/read persistence, commitment fallback, pool availability/spend, realised reporting, weighted Track cohorts including incomplete R&D, chart catalog/saved definitions/unknown propagation, migration/model consistency and immutable snapshots. ESI refresh preservation tests now include all three new recorded fields; existing job association/R&D/verdict workflows remain covered.
- Added a real API/database/browser workflow covering T2 Run creation, cost/capital edits, reload persistence, small ratio display, selected Track KPI persistence, saved chart configuration/reload, clearing inputs to unknown, and immutable snapshot display after Run edits. All 25 Playwright workflows passed; after the final aggregation arithmetic adjustment, both affected Run-accounting and Track-reporting workflows passed again.
- Final verification: `dotnet build --no-restore` passed with zero warnings/errors; `dotnet test --no-restore` passed all 104 tests; `npm run build` passed; `dotnet ef migrations has-pending-model-changes --project src/GhostWatch.Api --no-build` reports no changes. Generated upgrade SQL was reviewed for additive nullable columns only; isolated legacy migration tests report no pending migrations/model changes.
- `python3 scripts/verify-container.py` passed the production image build, non-root runtime, UI/deep links, API, migration/startup and named-volume persistence across recreation. Only the script's temporary Compose project/volume were recreated and removed. The user's live database, running deployment and credentials were not inspected or altered; no new live EVE verification is claimed.
- Updated the existing financial/chart documentation and canonical implementation status. Earlier log history and acceptance/verification/external notes remain intact. Both requested fidelity requirements are implemented and verified; no external blocker remains for this repository task.

## Economic Plan v1 browser-audit follow-up — Needs Attention acknowledgements

### Audit classification before implementation

Reviewed `v1-economic-plan.md`, current status/history and financial documentation, then the minimal API/EF SQLite reporting, Angular overview, management CRUD, migrations and tests. `EconomicReporting.Attention` generated deterministic links with no persisted acceptance state. User-created Playbooks, Records, Track KPI selections and Objective checklists live in separate database tables; there is no Economic Plan seed/import or name-based canonical record configuration in the repository.

| Finding | Classification | Repository work / remaining live correction |
|---|---|---|
| 1. T2 Playbook and monthly-review Record say capital efficiency needs manual calculation | Live database/document configuration | No equivalent stale manual-calculation claim was found in repository documentation. Clarified the plan's native `Capital Efficiency = Actual Profit / Slot Days / Capital Tied Up` calculation and display label. The two live documents still need editing. |
| 2. Capital efficiency not selected for T2 Workshop | Live dashboard configuration | The existing generic catalog and persisted KPI selector already support `capitalEfficiency`; no default selection or named-Track change is appropriate. Select **Profit / Slot-Day / ISK Tied Up** in the live T2 Workshop. |
| 3. T2 procedure omits Other Cost | Live procedure data **and repository documentation omission** | Updated the repository's T2 product-test procedure to include Expected/Actual Input, Job and Other Cost where applicable, revenue, manufacturing duration/concurrent slots, time to sell, Capital Tied Up, Profit per Slot-Day, capital efficiency and Scale / Retest / Drop. The live Playbook requires a separate edit. |
| 4. Tengu production-map Objective is still 0/4 | Live checklist data | Review the existing live evidence and mark BOM, capability gaps and internal/external sourcing as satisfied where supported; facility/job eligibility remains outstanding. No inferred checklist completion or name-based mutation was added. |
| 5. Unlinked Planning Test Track remains | Live Track data | Archive/remove the unwanted live record through the normal workflow after confirming its identity. No Track name matching, special migration or automatic archive was added. |
| 6. Intentional over-allocation still appears actionable | Application feature gap | Added generic persisted acknowledgement and Restore actions; the financial condition and all original rule predicates remain unchanged. |

### Architecture, persistence and identity

- `AttentionItem` now exposes a deterministic key built from rule, subject type and subject ID. Run missing-sales/verdict, Objective checklist/overdue and pool-utilisation rules carry their individual GUIDs. Rules on the same subject remain independent. Identical names/shared navigation paths cannot cause unrelated entities to share acceptance.
- Existing aggregate unassociated-job and wallet-freshness rules, plus over-allocation, retain programme scope. Accepting those findings accepts the whole programme condition, including changing counts/characters. No arbitrary user-defined rules or economic-plan constants were added.
- Migration `20260923181311_AddAttentionAcknowledgements` adds only an acknowledgement table and unique key index. Each row stores its own GUID, stable key/rule, optional subject type/ID, UTC timestamp and saved message/path for later inspection. It neither modifies nor backfills existing management, factual, financial or snapshot records.
- Acceptance persists until explicit Restore, even through a clear/recur interval. This avoids behaviour depending on whether a dashboard read happened during that interval. The API/UI always show whether the rule currently matches; cleared/deleted subjects retain saved context without a broken link. Renames keep identity, current matches use current generated messages, and a new entity with the same name gets a different GUID.
- No acknowledgement note editor was added; the small acceptance/timestamp/restore workflow is sufficient. ESI refresh continues to own factual tables only.

### API and UI

- `GET /api/economics/attention` reads active and acknowledged lists. The existing overview also exposes those partitions (`attention` and `acknowledged`) alongside unchanged programme summaries. Rules run normally before partitioning, and GET requests do not prune or mutate acceptance.
- `POST /api/economics/attention/acknowledgements` accepts only the key of a currently generated finding, validates and saves in a database transaction, and rejects absent/cleared/unsupported findings. Human-readable text and suppression scope cannot be supplied by the client. Duplicate/stale actions return 409.
- `DELETE /api/economics/attention/acknowledgements/{id}` removes that specific acceptance; its GUID acts as a generation token, so an old Restore request cannot delete a newer acceptance. Unique-key/concurrency conflicts return 409 with reload guidance.
- The existing Needs Attention panel gains Acknowledge, Show/Hide acknowledged, timestamps/current-match labels and Restore. The panel explains persistent acceptance, disables actions while saving/loading and reports success/errors. It does not alter capital, Track status, Objective completion, Run values or EVE data.

### Tests and verification

- Added backend coverage for normal over-allocation, persisted acceptance, active/acknowledged API partitions, direct underlying-rule evaluation, unchanged stored economic/factual state and native capital-efficiency results, real host restart with the same isolated on-disk database, and restore/reappearance.
- Parameterised identity coverage checks both Run rules, both Objective rules and pool utilisation with same-name entities; accepting one leaves every other matching rule/entity active, and renames update displayed context without losing acceptance.
- Covered clear/recur semantics, deleted-subject inspection, replacement-entity isolation, restore of a cleared finding, unsupported/nonmatching/invalid keys, duplicate acceptance and stale Restore after re-acceptance. Existing tests still cover all eight deterministic rules and Economic Plan calculations.
- Extended the ESI preservation fixture to acknowledge a generated finding and compare fresh stored acknowledgement rows together with all other management data through successful, failed, malformed and expired-job refreshes.
- Added a real API/database Playwright workflow for acknowledge → reload → inspect → restore → reload, independent same-rule entity visibility, unchanged Objective data and cleared-condition inspection/restore.
- Extended the isolated Compose smoke test to persist an acknowledgement across actual container recreation, check timestamp/matching state and unchanged Objective data, and restore the warning.
- Full browser verification exposed an existing chart-library initialization race: its delayed initial response could reset an already-entered definition name, leaving Save disabled in the Economic Plan accounting workflow. The editor/New chart action now wait for initial data before accepting input. Added a controlled delayed-schema-response browser regression; the accounting test itself remains unchanged.
- Final verification (2026-09-23): `dotnet build --no-restore` PASS with zero warnings/errors; `dotnet test --no-restore` PASS, all **112** tests; frontend `npm run build` PASS; full `npx playwright test` PASS, all **27** browser workflows, including the unchanged Economic Plan accounting/KPI/chart/snapshot workflow and the new acknowledgement and delayed-chart-load tests.
- `dotnet ef migrations has-pending-model-changes --project src/GhostWatch.Api --no-build` PASS, no pending changes. Reviewed generated upgrade SQL from `AddRunJobCostAndCapitalEfficiency`: new table/index and migration-history insert only, with no existing-data changes. Existing isolated legacy upgrade tests and the restart test verify applied migrations/model consistency and retained accounting/snapshot data.
- Final `python3 scripts/verify-container.py` PASS after the chart fix: production image build, non-root runtime, UI/deep links, API, migration/startup, named-volume preservation and acknowledgement persistence/restore across container recreation. Only the script's uniquely named temporary project/volume were recreated and removed. `git diff --check` PASS.

### Remaining live configuration

Findings 1–5 remain live-data corrections as described above; they are not unresolved application defects. Repository procedure documentation is corrected independently. The intentional live over-allocation warning can be acknowledged by the user once this version is deployed, but no acceptance was created in the live database. The user's database, credentials and running deployment were not edited, restarted or redeployed.
