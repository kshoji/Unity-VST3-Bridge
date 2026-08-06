#!/usr/bin/env bash
# Build VstHostNative.so (x86_64) and optionally install to Plugins/Linux/x86_64/.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
INSTALL=0

usage() {
  cat <<'EOF'
Usage: ./Build.sh [--Install] [--Debug|--Release]

  --Install     Copy VstHostNative.so to Plugins/Linux/x86_64/
  --Debug       Build Debug
  --Release     Build Release (default)

Environment overrides: CONFIGURATION
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -Install|--Install) INSTALL=1; shift ;;
    -h|--help) usage; exit 0 ;;
    --Debug) CONFIGURATION=Debug; shift ;;
    --Release) CONFIGURATION=Release; shift ;;
    *)
      echo "Unknown argument: $1"
      usage
      exit 1
      ;;
  esac
done

BUILD_DIR="${SCRIPT_DIR}/build"
PLUGIN_DIR="${SCRIPT_DIR}/../../Plugins/Linux/x86_64"

echo "=== VstHostNative Build (Linux) ==="
echo "Configuration : ${CONFIGURATION}"
echo "Build Dir     : ${BUILD_DIR}"

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
)

if command -v ninja >/dev/null 2>&1; then
  cmake "${CMAKE_ARGS[@]}" -G Ninja
else
  cmake "${CMAKE_ARGS[@]}" -G "Unix Makefiles"
fi

cmake --build "${BUILD_DIR}" --config "${CONFIGURATION}" --parallel

SO_SRC="${BUILD_DIR}/bin/VstHostNative.so"
if [[ ! -f "${SO_SRC}" ]]; then
  if [[ -f "${BUILD_DIR}/bin/${CONFIGURATION}/VstHostNative.so" ]]; then
    SO_SRC="${BUILD_DIR}/bin/${CONFIGURATION}/VstHostNative.so"
  else
    echo "ERROR: VstHostNative.so not found under ${BUILD_DIR}/bin"
    find "${BUILD_DIR}" -name 'VstHostNative.so' 2>/dev/null || true
    exit 1
  fi
fi

echo "Build succeeded: ${SO_SRC}"
file "${SO_SRC}" || true
ldd "${SO_SRC}" || true

if [[ "${INSTALL}" -eq 1 ]]; then
  mkdir -p "${PLUGIN_DIR}"
  DEST="${PLUGIN_DIR}/VstHostNative.so"
  cp -f "${SO_SRC}" "${DEST}"
  echo "Installed to: ${DEST}"
fi

echo "Done."
