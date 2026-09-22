# V1 implementation status

Source of truth: acceptance criteria in `initial-prompt.md`, plus subsequent user requests. A completed entity or scaffold alone is not a completed workflow. Detailed earlier history remains in `implementation.md`; ongoing notes are in `implementation-log.md`.

States: **done** = end-to-end implemented and automated verification recorded; **in progress** = active slice; **pending** = unfinished; **blocked** = genuine external dependency. Automated verification is distinct from live EVE verification.

| # | Original acceptance criterion | State | Evidence / remaining work |
|---|---|---|---|
| 1 | Launch locally | done | Production build and isolated Podman Compose/persistence smoke test |
| 2 | Authenticate multiple characters | done | SSO integration tests; user reports manual character workflow verification |
| 3 | Refresh characters through ESI | done | Queue, retries, scope checks, retained facts; integration tests |
| 4 | Current economic ESI data | done | Named/searchable skills, queue, wallets, orders, jobs, assets, blueprints, PI colony summaries, standings and LP; fixture/browser verification |
| 5 | Trained/currently usable capability | done | Reference-based foundations and evidence, active unknowns and subscription context; backend tests |
| 6 | Manual account grouping | done | Account editor, per-character group selection; API/browser coverage |
| 7 | Account Alpha/Omega state | done | Explicit Unknown/Alpha/Omega, manual account setting |
| 8 | Create Economy Tracks | done | CRUD, archive/restore, revision protection, browser tests |
| 9 | Link Characters to Tracks | done | Multi-Track links, economic assignment and planning notes; linked-character view on Track |
| 10 | Create Playbooks | done | Linked procedures, status/tags and Run selection |
| 11 | Markdown Playbook editing | done | Sanitized preview, retained Markdown/timestamps and stale-edit protection |
| 12 | Arbitrary Records/notes | done | Custom types/tags, Markdown and named links across management objects |
| 13 | Objectives and Gates | done | Manual checklist, progress, Track links and completion state; API/browser tests |
| 14 | Conceptual Capital Pools | done | Create/edit/archive/restore, roles, targets and Track defaults; Run-derived metrics await Runs |
| 15 | Capital adjustments/transfers | done | Atomic conceptual movements with immutable named history |
| 16 | Compare allocation to real wallets | done | Over-allocation warning, complete/partial/stale wallet states |
| 17 | View ESI industry jobs | done | Named products and retained job history on character detail |
| 18 | Identify unassociated jobs | done | Named industry-job table and unassociated filter |
| 19 | Run from ESI job | done | Prefilled form and atomic create/association endpoint |
| 20 | Associate jobs with Runs | done | Multiple jobs per Run; explicit remove/reassociate; preserved through refresh |
| 21 | Manual Runs | done | Creation/editing, manual type/purpose/status and retained notes |
| 22 | Expected financial results | done | Independent nullable input/other cost/revenue and calculated profit |
| 23 | Actual financial results | done | Independent actuals, margin, durations and efficiency |
| 24 | Run verdicts | done | R&D completion and successful verdict work without revenue |
| 25 | Run/Track metrics | done | Central aggregates, shared-pool semantics, selected Track KPIs and manual stages |
| 26 | Replacement Packages | done | Manual estimates, notes, edit and atomic default selection |
| 27 | Replacement coverage | done | Treasury-role allocation divided by package value; missing treasury stays unknown |
| 28 | Manual Economic Snapshot | pending | Immutable stored values |
| 29 | Automatic monthly snapshot | pending | Calendar-month deduplication, catch-up on start |
| 30 | Snapshot history | pending | |
| 31 | Validated chart JSON | pending | Explicit fields/sources; no arbitrary code/SQL |
| 32 | Example chart JSON | pending | |
| 33 | Chart preview | pending | |
| 34 | Dashboard chart placement | pending | |
| 35 | Reuse definition across pages | pending | |
| 36 | Track chart placements | pending | |
| 37 | Drag/drop chart ordering | pending | |
| 38 | Persistent chart order | pending | |
| 39 | Chart width | pending | Small/Medium/Wide |
| 40 | Dashboard headline metrics | done | Four concrete metrics from recorded finance; explicit unknowns |
| 41 | Active Tracks | done | Named programmes with allocation, commitments and 30-day P/L |
| 42 | Deterministic Needs Attention | done | Documented small rule set, actionable named links |
| 43 | Configurable charts | pending | Reusable renderer, line/bar/stacked/donut/KPI |
| 44 | Operational Track detail | in progress | Financials/KPIs, Runs, relevant jobs, Objectives, knowledge, Characters/stages connected; chart area remains |
| 45 | Preserve management through ESI refresh | in progress | Existing Track/notes safety tested; test new relationships as added |

## Additional accepted requests

- [done] Compose is the sole container startup workflow; host Podman via Distrobox on this system.
- [done] Tracked `.env.example`, ignored real credentials/runtime data.
- [done] Refresh all characters; live per-character spinner, stage and stage count.
- [done] Names in place of bare display IDs for current tables, explicit unavailable-name fallbacks.
- [done] Verified per-character grants, central missing-scope calculation, compact badge and targeted re-authorisation.
- [pending] Final full specification audit, complete tests/builds, secret/tracked-file review and final report.

## External verification

- [blocked] Live targeted re-authorisation and newly added EVE sections need user interaction/real-service verification. Fixtures do not establish live success. Continue all independent work.
- The user has already reported manual verification of the original character workflow. Do not repeat that as an unverified initial SSO setup blocker.

## Explicit exclusions

The original scope exclusions remain in force: no live pricing/scanning, SDE production trees, automated trading/purchasing/skill planning, inventory reservations, external killboard/Discord/corporation data, reimbursements, accounting ledger, AI advice, arbitrary SQL/JavaScript, freeform dashboard canvas or project-management engine.
