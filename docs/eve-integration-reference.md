# EVE integration reference review

Inspected read-only: `/home/wellsey/Dev/eve-economic-snapshot-exporter`, commit `6a2726a`. The reference working tree was clean when reviewed. No database, migrations, credentials, build output or Git history were copied. The inspected `SsoClient` helper was subsequently selectively ported and adapted for Ghost Watch.

This document records observed behaviour in that repository. Ghost Watch now implements the SSO milestone below; ESI remains pending. Register the new application and callback URI using `eve-sso-setup.md` before live verification.

## Useful source files

All paths below are relative to the reference repository's `src/EveSnapshot.Api` directory.

| File | Behaviour to reuse selectively |
| --- | --- |
| `Controllers/AuthController.cs` | Random state, S256 PKCE, ten-minute pending login, HTTP-only SameSite=Lax browser-binding cookie, consume state before exchange, character upsert |
| `Esi/SsoClient.cs` | OAuth metadata, trusted HTTPS login host, Basic client authentication, JWT verification and encrypted refresh tokens |
| `Models/EveOptions.cs` | Scope inventory and ESI compatibility-date configuration |
| `Esi/EsiClient.cs` | Bearer requests, cache partitioning, pagination, retry/backoff and shared ESI error-budget cooldown |
| `Services/RefreshService.cs` | Per-character orchestration, per-section attempt/success timestamps, stale-data retention on failure |
| `Services/CharacterGate.cs` | Serialise work for one character to prevent concurrent refresh-token rotation |
| `Services/CapacityCalculator.cs` | Separate trained and active capacity, unknown active state when required skill data is missing |
| `Services/EconomicAssessor.cs` | Evidence-backed broad foundations; never claims recipe-specific eligibility |
| `Services/AssetClassifier.cs` | Exact inventory categories and available vs fitted/contained vs unknown availability |
| `Esi/NameResolver.cs` | Candidate for a later selective review of name/category enrichment |
| `Entities/Entities.cs` | Stable EVE character IDs, user-managed account grouping/Omega state, factual section metadata |
| `Program.cs` | Persist data-protection keys; suppress HTTP logging that could expose authentication data |

## Authentication behaviour observed

1. Discover `https://login.eveonline.com/.well-known/oauth-authorization-server`; cache metadata and signing keys for 15 minutes. Reject endpoints outside HTTPS `login.eveonline.com`.
2. Generate state, PKCE verifier and a separate browser nonce. Bind callback to the initiating browser and expire the pending login after ten minutes.
3. Exchange the authorisation code using Basic client credentials, the verifier and exact callback URI. Do not send duplicate credentials in the request body.
4. Validate signed RS256 JWTs, trusted issuer, expiry, and both client-ID and `EVE Online` audiences. Reload signing keys once on an unknown signing key.
5. Parse the `CHARACTER:EVE:` subject, name and owner hash. Persist only protected refresh tokens; keep access tokens in memory.
6. Refresh under a per-character gate, validate identity and owner again, and save any rotated refresh token. Unreadable tokens require reconnection.
7. Emit known error codes, never raw token responses, exception bodies or callback query strings.

Ghost Watch must have its own data-protection application identity, key directory and database. Live verification requires the user to authenticate at CCP; no claim of successful SSO can be made from local unit tests alone.

## ESI endpoints observed

Paths are relative to `https://esi.evetech.net/`. `{id}` is a stable EVE character ID. The reference uses a configurable `X-Compatibility-Date`, an identifying User-Agent and English names.

| Data | Endpoint | Scope | Pagination in reference |
| --- | --- | --- | --- |
| Identity | `characters/{id}` | Public | Single response |
| Skills | `characters/{id}/skills` | `esi-skills.read_skills.v1` | Single response |
| Skill queue | `characters/{id}/skillqueue` | `esi-skills.read_skillqueue.v1` | Single response |
| Industry jobs | `characters/{id}/industry/jobs?include_completed=true` | `esi-industry.read_character_jobs.v1` | Single response |
| Blueprints | `characters/{id}/blueprints` | `esi-characters.read_blueprints.v1` | All `X-Pages` pages |
| Assets | `characters/{id}/assets` | `esi-assets.read_assets.v1` | All `X-Pages` pages |
| Wallet balance | `characters/{id}/wallet` | `esi-wallet.read_character_wallet.v1` | Single response |
| Market orders | `characters/{id}/orders` | `esi-markets.read_character_orders.v1` | Single response |
| Standings | `characters/{id}/standings` | `esi-characters.read_standings.v1` | Single response |
| Loyalty points | `characters/{id}/loyalty/points` | `esi-characters.read_loyalty.v1` | Single response |
| PI colonies | `characters/{id}/planets` | `esi-planets.manage_planets.v1` | Colony list only in refresh service |

The reference additionally reads wallet transactions and the first journal page with the wallet scope, and requests `esi-universe.read_structures.v1` for structure enrichment. These are not automatically part of Ghost Watch's first integration slice. The reference refresh service does not itself fetch detailed PI pins/routes; inspect actual needs before adding them.

Raw ESI payloads in the reference are handled as JSON nodes, rather than a complete typed DTO hierarchy. Refresh stages save collected sections independently. Pagination builds a complete replacement in memory before committing it, so a late page failure does not destroy the previous complete result. 401/403 errors require reconnection or scopes; 420/429 responses trigger shared cooldown, with bounded retries for rate limits, network errors and server errors. Cached private responses are partitioned by character and an access-token hash.

## Boundaries for the new application

The exporter persists character snapshot sections because it produces exports. Ghost Watch should preserve raw factual payloads where useful, but give industry jobs stable `(CharacterId, JobId)` identity and separate local Run associations. Do not cascade refresh deletion into local management tables or interpret jobs disappearing from ESI's time window as permission to delete history.

Future local account state, assignments, Track links and Run annotations must be separate from replaceable ESI facts. Economic assessment must distinguish trained levels, ESI active levels and manually recorded subscription state. An invention foundation is not proof that a character can use a particular recipe. Category logic must retain the fitted/contained distinction and avoid substring guesses for T3 materials.

## Implemented adaptation

`Eve/Auth/SsoClient.cs` selectively ports the inspected metadata, Basic token exchange, JWT verification and refresh-token helper with Ghost Watch namespaces, context and data-protection purpose. The callback and state store were written around the new model, with atomic state consumption and explicit public character projections. Changed ownership is rejected rather than clearing exporter snapshot sections. No exporter snapshot architecture was adopted.

Character connections and the refresh-token service pass signed-token fake-provider tests. Live login and refresh are unverified; ESI clients, data sections, economic assessments and name enrichment have not yet been ported. Current SSO guidance was also checked against [EVE's current documentation](https://developers.eveonline.com/docs/services/sso/).
