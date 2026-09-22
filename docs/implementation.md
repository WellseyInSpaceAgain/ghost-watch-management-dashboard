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

Next milestone: configure new-application SSO credentials/callback, implement the inspected PKCE/JWT/token-persistence patterns, and complete an interactive character login. Do not mark live SSO or multi-character ESI verification complete using mocked tests.

Docker launch remains unverified because neither Docker nor Podman is installed in the development environment. The backend and compiled frontend can be built and run directly.
