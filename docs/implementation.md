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

Next milestone: complete interactive login verification after local registration, then implement the ESI client and factual character refresh incrementally. Do not mark live SSO or multi-character ESI verification complete using mocked tests.

Host Podman is available through `distrobox-host-exec` from this Distrobox. The image builds and runs as a non-root user; migrations and named-volume persistence pass the repeatable container smoke test. Docker Compose itself has not been executed. The backend and compiled frontend also build and run directly.
