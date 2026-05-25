#!/usr/bin/env bash

set -euo pipefail

RID="${RID:-linux-x64}"
OUTPUT_DIR="artifacts/publish/${RID}"
APPDIR="artifacts/AppDir"
APPIMAGE_TOOL_URL="https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
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

cp "${OUTPUT_DIR}/ObeliskLauncher" "${APPDIR}/usr/bin/"

cat << 'EOF' > "${APPDIR}/AppRun"
#!/bin/sh
HERE="$(dirname "$(readlink -f "${0}")")"
exec "${HERE}/usr/bin/ObeliskLauncher" "$@"
EOF
chmod +x "${APPDIR}/AppRun"

cat << 'EOF' > "${APPDIR}/ObeliskLauncher.desktop"
[Desktop Entry]
Type=Application
Name=Obelisk Launcher
Exec=ObeliskLauncher
Icon=obelisklauncher
Categories=Game;
Terminal=false
EOF
cp "${APPDIR}/ObeliskLauncher.desktop" "${APPDIR}/usr/share/applications/"

ICON_FILE="assets/icon.png"
if [ -f "$ICON_FILE" ]; then
    cp "$ICON_FILE" "${APPDIR}/usr/share/icons/hicolor/256x256/apps/obelisklauncher.png"
    cp "$ICON_FILE" "${APPDIR}/obelisklauncher.png"
else
    echo "Warning: No icon found at $ICON_FILE. Generating a dummy one..."
    convert -size 256x256 xc:transparent "${APPDIR}/obelisklauncher.png" 2>/dev/null || touch "${APPDIR}/obelisklauncher.png"
fi

if [ ! -f "${APPIMAGE_TOOL}" ]; then
    echo "==> Downloading appimagetool..."
    curl -L "${APPIMAGE_TOOL_URL}" -o "${APPIMAGE_TOOL}"
    chmod +x "${APPIMAGE_TOOL}"
fi

echo "==> Building AppImage..."
ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 "./${APPIMAGE_TOOL}" --appimage-extract-and-run \
    "${APPDIR}" "artifacts/ObeliskLauncher-x86_64.AppImage"
chmod +x "artifacts/ObeliskLauncher-x86_64.AppImage"

echo "==> Creating standalone Linux tarball..."
tar -czf "artifacts/ObeliskLauncher-linux-x86_64.tar.gz" -C "${OUTPUT_DIR}" ObeliskLauncher

echo "==> Done!"
echo "    AppImage : artifacts/ObeliskLauncher-x86_64.AppImage"
echo "    Tarball  : artifacts/ObeliskLauncher-linux-x86_64.tar.gz"