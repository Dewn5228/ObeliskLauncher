#!/usr/bin/env bash

set -euo pipefail

RID="${RID:-linux-x64}"
OUTPUT_DIR="artifacts/publish/${RID}"
APPDIR="artifacts/AppDir"
APPIMAGE_TOOL_URL="https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage"
APPIMAGE_TOOL="artifacts/appimagetool.AppImage"

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd)"

cd "${REPO_ROOT}"

echo "==> Publishing .NET project for Linux..."
./scripts/publish-linux.sh

echo "==> Setting up AppDir..."
rm -rf "${APPDIR}"
mkdir -p "${APPDIR}/usr/bin"
mkdir -p "${APPDIR}/usr/share/applications"
mkdir -p "${APPDIR}/usr/share/icons/hicolor/256x256/apps"

cp "${OUTPUT_DIR}/TEKLauncher" "${APPDIR}/usr/bin/"

cat << 'EOF' > "${APPDIR}/AppRun"
#!/bin/sh
HERE="$(dirname "$(readlink -f "${0}")")"
exec "${HERE}/usr/bin/TEKLauncher" "$@"
EOF
chmod +x "${APPDIR}/AppRun"

cat << 'EOF' > "${APPDIR}/TEKLauncher.desktop"
[Desktop Entry]
Type=Application
Name=TEKLauncher
Exec=TEKLauncher
Icon=teklauncher
Categories=Game;
Terminal=false
EOF
cp "${APPDIR}/TEKLauncher.desktop" "${APPDIR}/usr/share/applications/"

ICON_FILE="Assets/icon.png"
if [ -f "$ICON_FILE" ]; then
    cp "$ICON_FILE" "${APPDIR}/usr/share/icons/hicolor/256x256/apps/teklauncher.png"
    cp "$ICON_FILE" "${APPDIR}/teklauncher.png"
else
    echo "Warning: No icon found at $ICON_FILE. Generating a dummy one..."
    convert -size 256x256 xc:transparent "${APPDIR}/teklauncher.png" 2>/dev/null || touch "${APPDIR}/teklauncher.png"
fi

if [ ! -f "${APPIMAGE_TOOL}" ]; then
    echo "==> Downloading appimagetool..."
    curl -L "${APPIMAGE_TOOL_URL}" -o "${APPIMAGE_TOOL}"
    chmod +x "${APPIMAGE_TOOL}"
fi

echo "==> Building AppImage..."
ARCH=x86_64 ./${APPIMAGE_TOOL} "${APPDIR}" "artifacts/TEKLauncher-x86_64.AppImage"

echo "==> Success! AppImage created at: artifacts/TEKLauncher-x86_64.AppImage"
