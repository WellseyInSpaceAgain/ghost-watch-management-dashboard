# Container operation with Compose

Use the repository's `compose.yaml` for startup, configuration and persistence.

```bash
podman compose up -d --build
```

Or with Docker:

```bash
docker compose up -d --build
```

After the initial build, `podman compose up -d` / `docker compose up -d` starts the existing image. Use `--build` after source changes. Open http://localhost:8080. Compose reads SSO settings from the ignored `.env` file described in [EVE setup](eve-sso-setup.md).

## This Distrobox environment

Podman is on the host, so invoke its Compose provider through Distrobox:

```bash
distrobox-host-exec podman compose up -d --build
distrobox-host-exec podman compose logs -f
distrobox-host-exec podman compose down
```

The workspace is shared with the host. The host's `podman compose` delegates to its installed Compose provider; no separate hand-written `podman run` command is needed.

The service runs as the non-root .NET `app` user. A Compose-managed named volume persists `/app/data`, including SQLite and the data-protection keys. `compose down` preserves that volume. Do not add `--volumes` to normal shutdown unless you intend to delete your data.

## Repeatable smoke test

```bash
python3 scripts/verify-container.py
```

The script uses this same Compose file under a uniquely named test project, with an automatically assigned loopback port and empty SSO credentials. It builds and starts with `compose up -d --build`, checks health/UI/API/non-root execution, then uses `compose up -d --force-recreate` to verify volume persistence. Finally it removes only that test project's resources, including its test volume. `--skip-build` reuses the existing image.

Stop the normal application before backing up its volume; preserve both the database and keys. `GHOST_WATCH_PORT` can override the default 8080 host port. If changed for normal use, also update the registered and configured SSO callback URL.
