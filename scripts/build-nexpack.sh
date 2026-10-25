#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd)"

RID="${RID:-linux-x64}"
OUTPUT_DIR="${REPO_ROOT}/artifacts/publish/${RID}"
BUILD_DIR="${REPO_ROOT}/artifacts/nexpack"
BOOTSTRAP_DIR="${REPO_ROOT}/artifacts/nexpack-bootstrap"

NEXPACK_RELEASE_URL="https://github.com/SimaoMoreira5228/nexpack/releases/latest/download"

cd "${REPO_ROOT}"

echo "==> Publishing .NET project for Linux..."
./scripts/publish-linux.sh

echo "==> Setting up Nexpack staging..."
rm -rf "${BUILD_DIR}"
mkdir -p "${BUILD_DIR}/staging/usr/bin"

cp "${OUTPUT_DIR}/ObeliskLauncher" "${BUILD_DIR}/staging/usr/bin/"

if [ -f "assets/icon.png" ]; then
    mkdir -p "${BUILD_DIR}/staging/usr/share/icons/hicolor/256x256/apps"
    cp "assets/icon.png" "${BUILD_DIR}/staging/usr/share/icons/hicolor/256x256/apps/obelisklauncher.png"
fi

echo "==> Downloading nexpack bootstrap binaries from GitHub releases..."
mkdir -p "${BOOTSTRAP_DIR}"
NEXPACKD_BIN="${BOOTSTRAP_DIR}/nexpackd"
NXPK_BIN="${BOOTSTRAP_DIR}/nxpk"

curl -fsSL "${NEXPACK_RELEASE_URL}/nexpackd-linux-x86_64" -o "${NEXPACKD_BIN}"
curl -fsSL "${NEXPACK_RELEASE_URL}/nxpk-linux-x86_64" -o "${NXPK_BIN}"
chmod +x "${NEXPACKD_BIN}" "${NXPK_BIN}"

echo "==> Generating spec.toml..."
cat > "${BUILD_DIR}/spec.toml" << SPEC
[app]
id = "io.obelisklauncher"
version = "1.3.9"
entrypoint = "/usr/bin/ObeliskLauncher"

# Permissions declared for sandboxed mode. The app runs
# without sandbox by default (--no-sandbox is the default).
# Use --sandbox to enable the bubblewrap sandbox.
[permissions]
network = true
filesystem = ["\$HOME"]
devices = ["dri", "audio"]
ipc = ["wayland", "dbus-session", "x11"]
display = "wayland"

# Self-bootstrap: embed nexpackd + nxpk so users can
# chmod +x && ./obelisklauncher.nxpk with zero setup.
[bootstrap]
nexpackd = "${NEXPACKD_BIN}"
nxpk = "${NXPK_BIN}"

[[layer]]
role = "app"
source = "./staging"
SPEC

echo "==> Building Nexpack bundle..."
cd "${BUILD_DIR}"
"${NXPK_BIN}" pack spec.toml

mv ./*.nxpk "${REPO_ROOT}/artifacts/" 2>/dev/null || true

echo "==> Done!"
echo "    Bundle : artifacts/obelisklauncher.nxpk"
echo ""
echo "To run:"
echo "  chmod +x artifacts/obelisklauncher.nxpk && ./artifacts/obelisklauncher.nxpk"
echo "  # or: nxpk run artifacts/obelisklauncher.nxpk"
echo "  # or: nxpk run artifacts/obelisklauncher.nxpk --sandbox"
