#!/usr/bin/env python3
"""Build and smoke-test via local Podman or host Podman from Distrobox.

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
parser.add_argument("--image", default="localhost/ghost-watch-dashboard:dev")
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
podman = (["podman"] if shutil.which("podman") else ["distrobox-host-exec", "podman"])

def run(*arguments, capture=False):
    result = subprocess.run([*podman, *arguments], check=True, text=True, stdout=subprocess.PIPE if capture else None)
    return result.stdout.strip() if capture else None

run("--version")
if not args.skip_build:
    run("build", "-t", args.image, "-f", str(root / "Dockerfile"), str(root))
name = "ghost-watch-smoke-" + uuid.uuid4().hex[:12]
volume = name + "-data"
run("volume", "create", volume)
try:
    def start():
        run("run", "--detach", "--name", name, "--publish", "127.0.0.1::8080",
            "--volume", volume + ":/app/data", args.image)
        address = run("port", name, "8080", capture=True).splitlines()[0]
        base = "http://" + address
        for _ in range(120):
            try:
                with urllib.request.urlopen(base + "/api/health", timeout=2) as response:
                    assert response.status == 200
                return base
            except (OSError, TimeoutError):
                time.sleep(.25)
        run("logs", name)
        raise RuntimeError("Container did not become healthy")

    def get(path):
        with urllib.request.urlopen(base + path, timeout=5) as response:
            return response.read()

    base = start()
    assert run("exec", name, "id", "-u", capture=True) != "0", "Runtime must be non-root"
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
    run("rm", "--force", name)
    base = start()
    restored = json.loads(get("/api/economics/tracks/" + track["id"]))
    assert restored["notes"] == "Preserve this across container recreation"
    assert restored["createdAt"].endswith("Z")
    print("PASS: non-root container, UI/deep links, API, migrations and named-volume persistence across recreation.")
finally:
    subprocess.run([*podman, "rm", "--force", "--ignore", name], check=False)
    subprocess.run([*podman, "volume", "rm", volume], check=False)
