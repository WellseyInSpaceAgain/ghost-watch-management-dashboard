#!/usr/bin/env python3
"""Build and smoke-test compose.yaml via Docker/Podman Compose or host Podman from Distrobox.

Uses only its own uniquely named container and volume; cleans them up on exit.
"""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import time
import urllib.error
import urllib.request
import uuid

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--skip-build", action="store_true")
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
if shutil.which("podman"):
    engine = ["env", "GHOST_WATCH_PORT=0", "Eve__ClientId=", "Eve__ClientSecret=", "Eve__CallbackUrl=http://localhost:8080/api/auth/eve/callback", "podman"]
elif shutil.which("distrobox-host-exec"):
    engine = ["distrobox-host-exec", "env", "GHOST_WATCH_PORT=0", "Eve__ClientId=", "Eve__ClientSecret=", "Eve__CallbackUrl=http://localhost:8080/api/auth/eve/callback", "podman"]
else:
    engine = ["env", "GHOST_WATCH_PORT=0", "Eve__ClientId=", "Eve__ClientSecret=", "Eve__CallbackUrl=http://localhost:8080/api/auth/eve/callback", "docker"]
name = "ghost-watch-smoke-" + uuid.uuid4().hex[:12]
compose = [*engine, "compose", "--env-file", "/dev/null", "--project-name", name, "--file", str(root / "compose.yaml")]

def run(*arguments, capture=False):
    result = subprocess.run([*compose, *arguments], check=True, text=True, stdout=subprocess.PIPE if capture else None)
    return result.stdout.strip() if capture else None

run("version")
try:
    def start(build=False):
        run("up", "--detach", "--force-recreate", *(["--build"] if build else []))
        address = run("port", "ghost-watch", "8080", capture=True).splitlines()[0]
        base = "http://" + address
        for _ in range(120):
            try:
                with urllib.request.urlopen(base + "/api/health", timeout=2) as response:
                    assert response.status == 200
                return base
            except (OSError, TimeoutError):
                time.sleep(.25)
        run("logs", "ghost-watch")
        raise RuntimeError("Container did not become healthy")

    def get(path):
        with urllib.request.urlopen(base + path, timeout=5) as response:
            return response.read()

    base = start(build=not args.skip_build)
    assert run("exec", "-T", "ghost-watch", "id", "-u", capture=True) != "0", "Runtime must be non-root"
    assert b"Ghost Watch Management Dashboard" in get("/")
    assert b"<app-root>" in get("/tracks/new")
    config = json.loads(get("/api/auth/eve/config"))
    assert config["configured"] is False
    assert config["callbackUrl"] == "http://localhost:8080/api/auth/eve/callback"
    assert json.loads(get("/api/eve/characters")) == []
    try:
        get("/api/missing")
    except urllib.error.HTTPError as error:
        assert error.code == 404
    else:
        raise AssertionError("Unknown API routes must return 404")
    request = urllib.request.Request(base + "/api/economics/tracks", data=json.dumps({
        "name": "Container persistence check", "description": "", "status": "Active", "purpose": "Other",
        "notes": "Preserve this across container recreation",
    }).encode(), headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(request) as response:
        track = json.load(response)
    base = start()
    restored = json.loads(get("/api/economics/tracks/" + track["id"]))
    assert restored["notes"] == "Preserve this across container recreation"
    assert restored["createdAt"].endswith("Z")
    print("PASS: non-root container, UI/deep links, API, migrations and named-volume persistence across recreation.")
finally:
    subprocess.run([*compose, "down", "--volumes"], check=False)
