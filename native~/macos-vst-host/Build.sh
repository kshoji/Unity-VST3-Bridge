#!/usr/bin/env bash
# Build VstHostNative.bundle (Universal arm64+x86_64 by default) and optionally
# install to Plugins/macOS/.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
INSTALL=0
ARCHS="${ARCHS:-arm64;x86_64}"

usage() {
  cat <<'EOF'
Usage: ./Build.sh [--Install] [--Debug|--Release] [--Arch arm64|x86_64|universal]

  --Install     Copy VstHostNative.bundle to Plugins/macOS/
  --Debug       Build Debug
  --Release     Build Release (default)
  --Arch        arm64 | x86_64 | universal (default: universal)

Environment overrides: CONFIGURATION, ARCHS (semicolon-separated for CMake)
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -Install|--Install) INSTALL=1; shift ;;
    -h|--help) usage; exit 0 ;;
    --Debug) CONFIGURATION=Debug; shift ;;
    --Release) CONFIGURATION=Release; shift ;;
    --Arch)
      shift
      case "${1:-}" in
        arm64) ARCHS="arm64" ;;
        x86_64) ARCHS="x86_64" ;;
        universal|Universal|both|Both) ARCHS="arm64;x86_64" ;;
        *) echo "Unknown --Arch: ${1:-}"; usage; exit 1 ;;
      esac
      shift
      ;;
    *)
      echo "Unknown argument: $1"
      usage
      exit 1
      ;;
  esac
done

BUILD_DIR="${SCRIPT_DIR}/build"
PLUGIN_DIR="${SCRIPT_DIR}/../../Plugins/macOS"

echo "=== VstHostNative Build (macOS) ==="
echo "Configuration : ${CONFIGURATION}"
echo "Architectures : ${ARCHS}"
echo "Build Dir     : ${BUILD_DIR}"

# SMTG cmake requires XCODE_VERSION; Command Line Tools alone lack xcodebuild.
# Prefer a real Xcode.app when present; otherwise satisfy the version gate for CLT builds.
if [[ -z "${XCODE_VERSION:-}" ]]; then
  if command -v xcodebuild >/dev/null 2>&1 \
     && xcodebuild -version >/dev/null 2>&1; then
    :
  else
    export XCODE_VERSION="${XCODE_VERSION:-15.0}"
    echo "Note: using XCODE_VERSION=${XCODE_VERSION} (Command Line Tools / no full Xcode)."
  fi
fi

if [[ -z "${SDKROOT:-}" ]]; then
  if SDKROOT_CANDIDATE="$(xcrun --sdk macosx --show-sdk-path 2>/dev/null)"; then
    export SDKROOT="${SDKROOT_CANDIDATE}"
  fi
fi

# Fresh configure if a previous generator choice left a broken cache.
if [[ -f "${BUILD_DIR}/CMakeCache.txt" ]]; then
  PREV_GEN="$(grep -E '^CMAKE_GENERATOR:' "${BUILD_DIR}/CMakeCache.txt" 2>/dev/null | cut -d= -f2 || true)"
  WANT_GEN="Unix Makefiles"
  if command -v ninja >/dev/null 2>&1; then
    WANT_GEN="Ninja"
  fi
  if [[ -n "${PREV_GEN}" && "${PREV_GEN}" != "${WANT_GEN}" ]]; then
    echo "Generator changed (${PREV_GEN} → ${WANT_GEN}); clearing ${BUILD_DIR}"
    rm -rf "${BUILD_DIR}"
  fi
fi

mkdir -p "${BUILD_DIR}"

CMAKE_ARGS=(
  -S "${SCRIPT_DIR}"
  -B "${BUILD_DIR}"
  -DCMAKE_BUILD_TYPE="${CONFIGURATION}"
  -DCMAKE_OSX_ARCHITECTURES="${ARCHS}"
)

if command -v ninja >/dev/null 2>&1; then
  cmake "${CMAKE_ARGS[@]}" -G Ninja
else
  cmake "${CMAKE_ARGS[@]}" -G "Unix Makefiles"
fi

cmake --build "${BUILD_DIR}" --config "${CONFIGURATION}" --parallel

BUNDLE_SRC="${BUILD_DIR}/bin/VstHostNative.bundle"
if [[ ! -d "${BUNDLE_SRC}" ]]; then
  # Multi-config generators may nest by configuration
  if [[ -d "${BUILD_DIR}/bin/${CONFIGURATION}/VstHostNative.bundle" ]]; then
    BUNDLE_SRC="${BUILD_DIR}/bin/${CONFIGURATION}/VstHostNative.bundle"
  else
    echo "ERROR: bundle not found under ${BUILD_DIR}/bin"
    find "${BUILD_DIR}" -name 'VstHostNative.bundle' -type d 2>/dev/null || true
    exit 1
  fi
fi

echo "Build succeeded: ${BUNDLE_SRC}"
file "${BUNDLE_SRC}/Contents/MacOS/VstHostNative" || true

if [[ "${INSTALL}" -eq 1 ]]; then
  mkdir -p "${PLUGIN_DIR}"
  DEST="${PLUGIN_DIR}/VstHostNative.bundle"
  if [[ -e "${DEST}" ]]; then
    if ! rm -rf "${DEST}" 2>/dev/null; then
      echo "WARNING: could not remove locked ${DEST}. Built bundle remains at: ${BUNDLE_SRC}"
      exit 0
    fi
  fi
  cp -R "${BUNDLE_SRC}" "${DEST}"
  # Ad-hoc sign so Apple Silicon Editor can load the module locally.
  if command -v codesign >/dev/null 2>&1; then
    codesign --force --deep -s - "${DEST}" >/dev/null 2>&1 || \
      echo "WARNING: codesign ad-hoc failed (Editor may still load linker-signed binary)."
  fi
  echo "Installed to: ${DEST}"
fi

echo "Done."
