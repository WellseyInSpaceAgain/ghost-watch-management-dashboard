# Implementation progress

The complete brief is the product scope. Work proceeds in runnable, independently committed stages; scaffolding does not imply that the Economics module is complete.

## Decisions

- One ASP.NET Core application with feature folders; no speculative service layers or future modules.
- Angular routes follow operational workflows. Only implemented workflows appear in navigation.
- SQLite with new EF migrations. Local management and future ESI factual tables stay separate.
- No demo data in the real database. Missing metrics display an explanation, never a fabricated zero.
- Keep the existing `ghost-watch-management-dashboard` IDE workspace rather than moving an open project. Solution and application identity use Ghost Watch.
- Credentials, databases and data-protection keys belong outside source control.

## Stages

1. Runnable application shell, build/test tooling and clean repository.
2. First manual workflow: Economy Tracks with SQLite persistence, validation and archive retention.
3. EVE SSO, character persistence and explicit interactive login verification.
4. ESI client, per-character refresh and economic assessments, informed by the reference exporter.
5. Capital Pools, Runs and stable industry-job associations.
6. Playbooks, Records, Objectives/Gates and replacement packages.
7. Central financial calculations, immutable snapshots and monthly deduplication.
8. Validated chart definitions, reusable renderer, placements and persisted ordering.
9. Operational summaries, deterministic attention rules and complete end-to-end verification.

A small manual Track slice is independent of SSO and makes the foundation usable while EVE registration and live authentication are pending. Financial summaries and selected KPIs follow their source data; no unsupported calculations are shown.

## Verified first milestone

- .NET 10 API and Angular 22 / Angular Material production build.
- New SQLite model and `AddEconomyTracks` migration, applied on startup.
- Track creation, editing, archive filtering and restoration; preserved creation dates and notes.
- Optimistic revision checks reject stale updates with HTTP 409.
- Eight backend tests against isolated SQLite databases.
- Two Chromium workflow tests, including reload persistence, archive/restore, narrow-screen layout, API failure and retry.
- Published Release application verified with compiled frontend, deep-link routing, API 404 handling, UTC timestamps and SQLite persistence across process restart.
- EVE reference review recorded in `eve-integration-reference.md`; no EVE implementation has been ported yet.

## Character authentication milestone

- `EveCharacter` identity and protected refresh-token persistence, separate from management entities.
- Migration `AddEveCharacterAuthentication`.
- S256 PKCE, ten-minute browser-bound state and atomic single-use callback consumption.
- Trusted EVE metadata/JWKS discovery, signed RS256 token validation, issuer/audience/expiry checks and key-rollover retry.
- Protected refresh-token rotation under a per-character lock; access tokens remain in memory.
- Character list, reconnect without duplicate identities, explicit configuration and safe error states.
- Changed character ownership is rejected for manual review; local Track data is preserved.
- 23 backend tests and four browser tests pass. Live EVE authentication is not established by these fake-provider tests.
- `docs/eve-sso-setup.md` explains registration and local credentials; the user has not registered Ghost Watch yet.

Live integration verification remains pending local registration and actual character login. Do not mark live SSO or multi-character ESI verification complete using mocked tests.

Host Podman is available through `distrobox-host-exec` from this Distrobox. The image builds and runs as a non-root user; migrations and named-volume persistence pass the repeatable container smoke test. Startup and verification now use `compose.yaml` via host `podman compose`; the Compose provider and data persistence have been verified. The backend and compiled frontend also build and run directly.

## First ESI refresh milestone

- Selectively adapted the reference ESI HTTP client: bounded retries, shared rate/error-budget cooldown, token/character-isolated caching and complete-page collection.
- Added queued refresh for wallets, skills, skill queue, industry jobs and market orders, with duplicate suppression and per-character progress. Pending queue work is in memory and must be requested again after an application restart.
- `EveSection` stores only factual data with last-attempt/last-success/error metadata. Failed or malformed responses retain previous facts.
- `EveIndustryJob` uses `(CharacterId, JobId)` identity, raw payloads and last-seen timestamps. Refresh upserts facts; disappearance from ESI does not delete historical rows. No Run relationship exists yet.
- Migration `AddEveFactualData` creates the new factual tables without modifying Track data.
- Character data page shows wallet, trained/active capacity and jobs, plus inspectable raw records and section freshness. Missing active skills produce unknown active capacity. Subscription state, free slots and recipe eligibility are not inferred.
- 31 backend tests and five browser tests pass; Angular production build and Compose container smoke verification pass.
- Compose workflow includes tracked `.env.example`; real `.env` stays ignored. Tests use a unique Compose project, random loopback port and empty credentials.

Next: continue the remaining factual EVE sections, name/category enrichment, account grouping and broader economic assessments. Live SSO/ESI and multiple-character live refresh still require actual user configuration and login.
