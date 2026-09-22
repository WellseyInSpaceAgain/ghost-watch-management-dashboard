# V1 implementation status

Source of truth: acceptance criteria in `initial-prompt.md`, plus subsequent user requests. A completed entity or scaffold alone is not a completed workflow. Detailed earlier history remains in `implementation.md`; ongoing notes are in `implementation-log.md`.

States: **done** = end-to-end implemented and automated verification recorded; **in progress** = active slice; **pending** = unfinished; **blocked** = genuine external dependency. Automated verification is distinct from live EVE verification.

| # | Original acceptance criterion | State | Evidence / remaining work |
|---|---|---|---|
| 1 | Launch locally | done | Production build and isolated Podman Compose/persistence smoke test |
| 2 | Authenticate multiple characters | done | SSO integration tests; user reports manual character workflow verification |
| 3 | Refresh characters through ESI | done | Queue, retries, scope checks, retained facts; integration tests |
| 4 | Current economic ESI data | pending | Wallet, jobs, assets, blueprints exist; PI, standings, LP and readable skill/order views remain |
| 5 | Trained/currently usable capability | pending | Capacity limits done; broader economic foundations remain |
| 6 | Manual account grouping | in progress | Next vertical slice |
| 7 | Account Alpha/Omega state | in progress | Next vertical slice |
| 8 | Create Economy Tracks | done | CRUD, archive/restore, revision protection, browser tests |
| 9 | Link Characters to Tracks | in progress | Includes separate local economic assignment |
| 10 | Create Playbooks | pending | |
| 11 | Markdown Playbook editing | pending | Include revision retention |
| 12 | Arbitrary Records/notes | pending | Typed metadata, flexible Markdown and links |
| 13 | Objectives and Gates | pending | Checklist conditions, manual progress |
| 14 | Conceptual Capital Pools | pending | |
| 15 | Capital adjustments/transfers | pending | History required |
| 16 | Compare allocation to real wallets | pending | Unknown/incomplete wallets must remain explicit |
| 17 | View ESI industry jobs | done | Named products and retained job history on character detail |
| 18 | Identify unassociated jobs | pending | |
| 19 | Run from ESI job | pending | |
| 20 | Associate jobs with Runs | pending | Include removing associations |
| 21 | Manual Runs | pending | |
| 22 | Expected financial results | pending | |
| 23 | Actual financial results | pending | |
| 24 | Run verdicts | pending | R&D success independent of profit |
| 25 | Run/Track metrics | pending | Central calculations, selected KPIs and manual T3 stages |
| 26 | Replacement Packages | pending | |
| 27 | Replacement coverage | pending | |
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
| 40 | Dashboard headline metrics | pending | |
| 41 | Active Tracks | done | Overview table; financial columns await domain slices |
| 42 | Deterministic Needs Attention | pending | |
| 43 | Configurable charts | pending | Reusable renderer, line/bar/stacked/donut/KPI |
| 44 | Operational Track detail | pending | Current page edits metadata only |
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
