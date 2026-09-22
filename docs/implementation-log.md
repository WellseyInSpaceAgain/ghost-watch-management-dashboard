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
