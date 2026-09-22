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
