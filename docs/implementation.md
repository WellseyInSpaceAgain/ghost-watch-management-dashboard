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
