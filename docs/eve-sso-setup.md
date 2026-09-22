# EVE SSO setup

Ghost Watch now supports character login and protected refresh-token persistence. The user has manually verified the character workflow; these instructions remain available for new installations. After connecting, open a character and use Refresh EVE data to collect wallets, skills, skill queue, market orders, industry jobs, assets and blueprints.

## Register Ghost Watch

1. Open [My Applications in the EVE Developer Portal](https://developers.eveonline.com/applications) and sign in with your EVE account.
2. Create a new application named **Ghost Watch Management Dashboard**, with a description of this personal economics console. Choose authenticated API access if the form asks for a connection type.
3. Add the ten character scopes listed below. Do not select corporation scopes or unrelated permissions.
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
```

These scopes cover the character economics requested by the brief. They are granted at connection time; the application collects the seven sections above. Standings, loyalty points and PI refresh follow in a later slice.

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
