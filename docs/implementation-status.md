# V1 implementation status

Source of truth: acceptance criteria in `initial-prompt.md`, plus subsequent user requests. A completed entity or scaffold alone is not a completed workflow. Detailed earlier history remains in `implementation.md`; ongoing notes are in `implementation-log.md`.

Final audit states: **PASS** = end-to-end implemented and verified; **BLOCKED** = a genuine external dependency. All 45 application criteria pass achievable automated verification. Live verification boundaries are recorded separately below; no fixture result is presented as live EVE success.

| # | Original acceptance criterion | State | Evidence / remaining work |
|---|---|---|---|
| 1 | Launch locally | PASS | Production build and isolated Podman Compose/persistence smoke test |
| 2 | Authenticate multiple characters | PASS | SSO integration tests; user reports manual character workflow verification |
| 3 | Refresh characters through ESI | PASS | Queue, retries, scope checks, retained facts; integration tests |
| 4 | Current economic ESI data | PASS | Named/searchable skills, queue, wallets, orders, jobs, assets, blueprints, PI colony summaries, standings and LP; fixture/browser verification |
| 5 | Trained/currently usable capability | PASS | Reference-based foundations and evidence, active unknowns and subscription context; backend tests |
| 6 | Manual account grouping | PASS | Account editor, per-character group selection; API/browser coverage |
| 7 | Account Alpha/Omega state | PASS | Explicit Unknown/Alpha/Omega, manual account setting |
| 8 | Create Economy Tracks | PASS | CRUD, archive/restore, revision protection, browser tests |
| 9 | Link Characters to Tracks | PASS | Multi-Track links, economic assignment and planning notes; linked-character view on Track |
| 10 | Create Playbooks | PASS | Linked procedures, status/tags and Run selection |
| 11 | Markdown Playbook editing | PASS | Sanitized preview, retained Markdown/timestamps and stale-edit protection |
| 12 | Arbitrary Records/notes | PASS | Custom types/tags, Markdown and named links across management objects |
| 13 | Objectives and Gates | PASS | Manual checklist, progress, Track links and completion state; API/browser tests |
| 14 | Conceptual Capital Pools | PASS | Create/edit/archive/restore, roles, targets, Track defaults and Run-derived commitments/available capital |
| 15 | Capital adjustments/transfers | PASS | Atomic conceptual movements with immutable named history |
| 16 | Compare allocation to real wallets | PASS | Over-allocation warning, complete/partial/stale wallet states |
| 17 | View ESI industry jobs | PASS | Named products and retained job history on character detail |
| 18 | Identify unassociated jobs | PASS | Named industry-job table and unassociated filter |
| 19 | Run from ESI job | PASS | Prefilled form and atomic create/association endpoint |
| 20 | Associate jobs with Runs | PASS | Multiple jobs per Run; explicit remove/reassociate; preserved through refresh |
| 21 | Manual Runs | PASS | Creation/editing, manual type/purpose/status and retained notes |
| 22 | Expected financial results | PASS | Independent nullable input/other cost/revenue and calculated profit |
| 23 | Actual financial results | PASS | Independent actuals, margin, durations and efficiency |
| 24 | Run verdicts | PASS | R&D completion and successful verdict work without revenue |
| 25 | Run/Track metrics | PASS | Central aggregates, shared-pool semantics, selected Track KPIs and manual stages |
| 26 | Replacement Packages | PASS | Manual estimates, notes, edit and atomic default selection |
| 27 | Replacement coverage | PASS | Treasury-role allocation divided by package value; missing treasury stays unknown |
| 28 | Manual Economic Snapshot | PASS | Named/noted immutable versioned capture and stored detail view |
| 29 | Automatic monthly snapshot | PASS | UTC month uniqueness, transactional capture, startup catch-up and hourly checks |
| 30 | Snapshot history | PASS | Stored programme/pool/Track/KPI/objective values and factual completeness |
| 31 | Validated chart JSON | PASS | Strict typed schema and source-specific validation; rejects unknown properties/code/SQL |
| 32 | Example chart JSON | PASS | New editor starts with expected/actual sample; standalone JSON schema and docs |
| 33 | Chart preview | PASS | Debounced validated live preview, Track context and helpful errors |
| 34 | Dashboard chart placement | PASS | Dashboard chart area, shared definition selector and add/remove actions |
| 35 | Reuse definition across pages | PASS | Definition edits appear in multiple placements; removal retains definition |
| 36 | Track chart placements | PASS | Track chart area and CURRENT_TRACK context |
| 37 | Drag/drop chart ordering | PASS | CDK drag handles plus accessible ordering controls; actual browser drag tested |
| 38 | Persistent chart order | PASS | Atomic page layout update, revision conflicts and reload persistence |
| 39 | Chart width | PASS | Small/Medium/Wide stored per placement and responsive layout |
| 40 | Dashboard headline metrics | PASS | Four concrete metrics from recorded finance; explicit unknowns |
| 41 | Active Tracks | PASS | Named programmes with allocation, commitments and 30-day P/L |
| 42 | Deterministic Needs Attention | PASS | Documented small rule set, actionable named links |
| 43 | Configurable charts | PASS | One Chart.js renderer: line, bar, stacked bar, pie/donut and KPI; accessible data table |
| 44 | Operational Track detail | PASS | Financials/KPIs, capital, Runs/jobs, Objectives, knowledge, Characters/stages and reusable charts |
| 45 | Preserve management through ESI refresh | PASS | Full fresh-database comparison of all local management families after successful, failed and expired-job refreshes |

## Additional accepted requests

- [PASS] Compose is the sole container startup workflow; host Podman via Distrobox on this system.
- [PASS] Tracked `.env.example`, ignored real credentials/runtime data.
- [PASS] Refresh all characters; live per-character spinner, stage and stage count.
- [PASS] Names in place of bare display IDs for current tables, explicit unavailable-name fallbacks.
- [PASS] Verified per-character grants, central missing-scope calculation, compact badge and targeted re-authorisation.
- [PASS] Final original-specification audit, backend/frontend builds, 85 backend tests, 24 browser tests, migration coherence, isolated Compose smoke/persistence, tracked-file review and `v1-report.md`.

## External verification

- [PASS] User previously verified the original character workflow. Final read-only live inspection found six connected characters with known complete scope grants, and successful stored data without errors/warnings in all seven sections supported by the running older build.
- [BLOCKED] Live update/new EVE section verification: automatic approval review rejected stopping the live container, copying its database/encryption keys to a repository-local backup and restarting with the new build. The command did not execute. Explicit approval of the brief service interruption and private backup location is required; the live container remains on its earlier build. Proposed backup: `src/GhostWatch.Api/App_Data/backups/pre-v1-upgrade/`, ignored by Git and restricted to the current user.
- [BLOCKED] A fresh targeted re-authorisation round trip requires user browser consent. Signed-token callback, missing-scope, wrong-character, cancellation and preservation tests pass; fresh live consent is not claimed.
- All independent v1 implementation and verification is complete. The prior approval-service sign-in failure was resolved by the user and is no longer a blocker.

## Explicit exclusions

The original scope exclusions remain in force: no live pricing/scanning, SDE production trees, automated trading/purchasing/skill planning, inventory reservations, external killboard/Discord/corporation data, reimbursements, accounting ledger, AI advice, arbitrary SQL/JavaScript, freeform dashboard canvas or project-management engine.

## Verification evidence

- `dotnet build --no-restore`: pass, zero warnings/errors.
- `dotnet test --no-restore`: 85 passed.
- `npm run build` and `npx playwright test` in `src/GhostWatch.Web`: production build and all 24 browser workflows pass.
- `dotnet ef migrations has-pending-model-changes --project src/GhostWatch.Api --no-build`: no pending changes.
- `python3 scripts/verify-container.py`: final isolated host Podman Compose image/startup/deep-link/migration/persistence check passes.
- Local Git identity matches the requested user/email; tracked-file review found no runtime/credential artifacts. Existing `.env` was not read.
- [Final report](v1-report.md) covers all 26 requested report topics. [Financial semantics](financial-metrics.md) and [chart contract](chart-configuration.md) document the implemented calculations and supported configuration.

## Economic Plan v1 fidelity

The follow-up implements the two remaining code-level requirements from `v1-economic-plan.md`. The original acceptance criteria, prior verification results, exclusions and external/live verification notes above are preserved.

| Requirement | State | Evidence |
|---|---|---|
| **Explicit Economic Run Job Cost and Capital Tied Up** | PASS | Nullable Expected Job Cost, Actual Job Cost and Capital Tied Up persist through API create/update/read/clear and Run form save/reload. Migration `20260923172146_AddRunJobCostAndCapitalEfficiency` adds nullable columns without defaults/backfill; isolated legacy upgrade tests preserve every existing financial input and snapshot JSON. Expected/actual total cost includes the separate Job Cost, with unknown propagation and explicit zero supported. Shared commitment, pool, realised/30-day, lifetime and R&D calculations use revised totals; Capital Tied Up remains independent. Backend, real browser and isolated container verification pass. |
| **Profit per Slot-Day per ISK of Capital Tied Up** | PASS | Central `FinancialMath`/`RunMetrics` calculation requires known actual profit, positive slot-days and explicit positive Capital Tied Up. Tests cover every missing/invalid denominator, zero/loss profits and capital independence. Saved Run financials, reporting, configurable Run charts and selected Track KPIs expose the ratio with appropriate precision/units. Track aggregation is `sum(realised profit) / sum(slot-days × explicit capital tied up)` over all Completed/Evaluated Runs; any incomplete Run makes it unknown. Weighted-cohort, chart, snapshot and browser tests pass. No Track-KPI architectural limitation remains. |

Follow-up verification (2026-09-23):

- `dotnet build --no-restore`: PASS, zero warnings/errors.
- `dotnet test --no-restore`: PASS, 104 tests.
- Frontend `npm run build`: PASS, production bundle.
- Frontend `npx playwright test`: PASS, all 25 browser workflows; the two affected accounting/reporting workflows also passed after the final weighted-aggregation adjustment.
- EF pending-model-change check: PASS; no pending changes. Generated upgrade SQL is additive only. Integration tests upgrade a pre-feature database without fabricated inputs and verify applied migration/model consistency.
- Snapshot compatibility: PASS; version-1 payloads/API reads/monthly identity stay unchanged, absent historical ratio values remain unknown, and version-2 captures store revised totals/KPIs immutably.
- `python3 scripts/verify-container.py`: PASS, isolated production image/startup/migrations/deep links/non-root/persistence smoke test.
- No live deployment/database changes or fresh live EVE verification were performed. Previous external verification notes remain separate from this completed repository follow-up.

Exact financial formulas, aggregation and migration implications are documented in [Financial metrics](financial-metrics.md); chart measure/format examples are in [Chart configuration](chart-configuration.md).
