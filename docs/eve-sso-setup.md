# EVE SSO setup

Ghost Watch now supports character login and protected refresh-token persistence. The user has manually verified the character workflow; these instructions remain available for new installations. After connecting, open a character and use Refresh EVE data to collect wallets, skills, skill queue, market orders, industry jobs, assets, blueprints, PI colonies, standings and loyalty points.

## Register Ghost Watch

1. Open [My Applications in the EVE Developer Portal](https://developers.eveonline.com/applications) and sign in with your EVE account.
2. Create a new application named **Ghost Watch Management Dashboard**, with a description of this personal economics console. Choose authenticated API access if the form asks for a connection type.
3. Add the eleven scopes listed below. Do not select corporation scopes or unrelated permissions.
4. Register the exact callback URL for the way you will run the app:

| Run mode | Callback URL |
| --- | --- |
| Podman / Docker on port 8080 | `http://localhost:8080/api/auth/eve/callback` |
| Angular development server on port 4200 | `http://localhost:4200/api/auth/eve/callback` |

5. Save the application and keep its client ID and secret locally. Do not reuse or copy runtime credentials from the exporter.

The callback must match the registered URL; requested scopes must be enabled for the registration. EVE handles password entry and consent. See the [current EVE SSO documentation](https://developers.eveonline.com/docs/services/sso/) for registration, redirect and permission requirements.

```text
esi-skills.read_skills.v1
esi-skills.read_skillqueue.v1
esi-industry.read_character_jobs.v1
esi-characters.read_blueprints.v1
esi-assets.read_assets.v1
esi-wallet.read_character_wallet.v1
esi-markets.read_character_orders.v1
esi-characters.read_standings.v1
esi-characters.read_loyalty.v1
esi-planets.manage_planets.v1
esi-universe.read_structures.v1
```

These scopes cover the character economics requested by the brief. They are granted at connection time; the application collects the ten sections above. PI refresh collects colony summaries.

## Configure a container

Copy the tracked template, then edit `.env` locally:

```bash
cp .env.example .env
chmod 600 .env
```

Fill in your client ID and secret in the ignored `.env` file:

```dotenv
Eve__ClientId=YOUR_CLIENT_ID
Eve__ClientSecret=YOUR_CLIENT_SECRET
Eve__CallbackUrl=http://localhost:8080/api/auth/eve/callback
```

Restrict the file with `chmod 600 .env`. Do not paste its contents into chat, logs or commits.

From Distrobox, use host Podman and the shared workspace path:

```bash
distrobox-host-exec podman compose up -d --build
```

From the host, use `podman compose up -d --build`, or `docker compose up -d --build` with Docker. Compose reads the `.env` configuration and manages the persistent volume. For subsequent starts without source changes, omit `--build`. Stop with `podman compose down` or `docker compose down` (through `distrobox-host-exec` here); keep the data volume.

## Configure development mode instead

Create ignored `src/GhostWatch.Api/appsettings.Local.json`:

```json
{
  "Eve": {
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET",
    "CallbackUrl": "http://localhost:4200/api/auth/eve/callback"
  }
}
```

Restrict its permissions with `chmod 600 src/GhostWatch.Api/appsettings.Local.json`. Restart the API after changing settings. Environment variables named `Eve__ClientId`, `Eve__ClientSecret` and `Eve__CallbackUrl` override the file.

Start the API and Angular app using the README. Use `http://localhost:4200`, including the `localhost` hostname, when starting login. Do not mix `localhost` and `127.0.0.1` between the login and callback: the browser-binding cookie must return to the same hostname. When switching run modes, update both the EVE registration and local callback setting.

## Verify a connection

1. Open the app and select **Characters**.
2. Confirm the setup panel has disappeared and choose **Log in with EVE Online**.
3. Sign in at EVE, select a character and consent to the requested permissions.
4. On return, confirm the character name and EVE ID appear in the list.
5. Repeat with another character, then reconnect an existing one and confirm no duplicate row appears.
6. Restart/recreate the application with the same data volume and confirm the list persists.

A restart during an unfinished login invalidates that pending login; simply start again. Browser state lasts ten minutes and is consumed once. A character ownership change is rejected for manual review, preserving the existing connection and local management history.

## Stored data and remaining verification

`EveCharacters` stores EVE identity and encrypted refresh tokens separately from `EconomyTracks`. API responses explicitly project public identity fields and never include tokens or owner hashes. Access tokens are retained only in memory; server-side refresh validates identity and saves rotated refresh tokens under a per-character lock.

The application uses its own ASP.NET Data Protection purpose and persists its key ring under `App_Data/keys` or `/app/data/keys`. The key ring is protected by filesystem access, not an external key-management service. Back up the entire stopped application's data directory/volume: the database alone is insufficient to decrypt tokens later. Losing the keys requires reconnecting characters.

Local tests exercise signed JWTs, callback state, PKCE, token protection and rotation against a fake EVE server. They do not establish that live SSO, live token refresh or multi-character ESI refresh works. Record those results only after actual verification. Use the character detail page to verify refresh, then repeat with a second character. Confirm last-success timestamps and retained facts after errors; do not mistake mocked test results for live verification.

The assets/blueprints feature has separate automated coverage; its new live inventory collection still needs verification after upgrading and refreshing. Existing character-workflow verification does not imply verification of every subsequent feature.

## Location names after upgrading

Public station/system names and industry product names are collected on refresh. For private structure names, add `esi-universe.read_structures.v1` to the existing EVE application registration, reconnect each character, then refresh. EVE may still deny a structure lookup when that character lacks access. The UI explicitly labels unavailable names and retains IDs as secondary references. Structure names are cached separately per character; container locations show the containing item type and its parent location.

## Per-character permission status

Ghost Watch persists the `scp` grants from the validated EVE access token, using the [EVE SSO JWT claims documentation](https://developers.eveonline.com/docs/services/sso/#jwt-token-claims). The requested scope list is not treated as proof of consent. Token refresh updates the known grants even if EVE does not rotate the refresh token.

After upgrading an existing database, grants remain unknown until the next successful token refresh or login. The Characters list marks these connections **Permissions not checked**. Each detail page has an **ESI Permissions** panel showing the status and any missing scope names.

To grant missing scopes:

1. Enable them in the EVE application registration.
2. Open the affected character's detail page and choose **Re-authorise Character**.
3. Select that same character in EVE and complete consent.
4. Confirm the success message and updated permissions, then refresh the character data.

Re-authorisation uses PKCE and browser-bound, single-use state with the intended character ID held server-side. A different returned character is rejected; failed or cancelled attempts retain the previous credentials and data. Successful re-authorisation updates the existing record by EVE character ID, invalidates its cached access token and leaves local management data intact. An expired or replayed login returns to Characters with an invalid-state message.

The required permissions and consuming operations are defined centrally in `EveScopes.Definitions`. New permissions need a definition and a consumer; the API computes missing scopes for both views. Refresh skips sections without the necessary grant and retains their last successful data. Structure enrichment is checked independently so missing structure permission does not prevent collecting inventory or resolving public station names.
