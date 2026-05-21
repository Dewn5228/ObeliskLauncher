#!/usr/bin/env bash

set -euo pipefail

RID="${RID:-linux-x64}"
CONFIGURATION="${CONFIGURATION:-Release}"
OUTPUT_DIR="${OUTPUT_DIR:-artifacts/publish/${RID}}"
SIGN_ASSEMBLY="${SIGN_ASSEMBLY:-false}"

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd)"

cd "${REPO_ROOT}"

dotnet publish TEKLauncher.csproj \
  -c "${CONFIGURATION}" \
  -r "${RID}" \
  /p:UiFramework=Avalonia \
  -p:SignAssembly="${SIGN_ASSEMBLY}" \
  --self-contained false \
  -p:PublishSingleFile=true \
  -o "${OUTPUT_DIR}"

printf 'Linux publish completed: %s\n' "${OUTPUT_DIR}"