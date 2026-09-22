# Containers from Distrobox

Podman 5.8.7 is available on the host. From this Distrobox use `distrobox-host-exec podman`; the workspace under `/home/wellsey/Dev/ghost-watch-management-dashboard` is shared with the host.

## Build and run

```bash
distrobox-host-exec podman build -t localhost/ghost-watch-dashboard:dev .
distrobox-host-exec podman volume create ghost-watch-data
distrobox-host-exec podman run -d --name ghost-watch \
  -p 127.0.0.1:8080:8080 \
  -v ghost-watch-data:/app/data \
  localhost/ghost-watch-dashboard:dev
```

Open http://localhost:8080. For SSO, configure credentials using [the setup guide](eve-sso-setup.md) and add `--env-file "$PWD/.env"` to the run command. Existing named containers must be stopped/removed before reusing their name; keep the data volume.

The image uses the non-root .NET `app` user. The named volume inherits the image data-directory ownership on first use; no privileged mode or world-writable permissions are needed. Using a named volume also avoids host bind-mount SELinux relabeling for runtime data.

## Repeatable smoke test

```bash
python3 scripts/verify-container.py
```

The script detects local Podman or falls back to host Podman, builds the image, and creates a uniquely named container and volume. It checks health, static UI, deep links, API errors, non-root execution and persistence after container recreation. It cleans up only its own temporary container and volume. Use `--skip-build` to verify the existing image.

Verified with host Podman: the initial image builds and launches, applies SQLite migrations, and retains a Track after recreation with the same volume. The first npm install encountered a transient connection reset; the retry succeeded. Docker Compose itself has not been executed; the shared Dockerfile has been verified with Podman.

## Preserve local data

Stop the application before backing up its data volume. Preserve both the SQLite database and the data-protection key ring. Removing the application container does not remove a named volume; removing the volume deletes the stored data.
