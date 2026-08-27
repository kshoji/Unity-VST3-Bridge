#!/usr/bin/env bash
# Clone the Steinberg VST3 SDK into ./vst3sdk for native rebuilds (developers only).
# UPM Git URL installs must not recurse into this tree (Windows path-length failures).
# End users use prebuilt Plugins/; rebuilders run this script once, then Build.sh / Build.ps1.
set -euo pipefail

COMMIT="${COMMIT:-58f8da7936800732561402d7936584ca4505de07}"
FORCE=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --force|-f) FORCE=1; shift ;;
    --commit) COMMIT="$2"; shift 2 ;;
    -h|--help)
      echo "Usage: $0 [--force] [--commit <sha>]"
      exit 0
      ;;
    *) echo "Unknown option: $1" >&2; exit 1 ;;
  esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEST="${SCRIPT_DIR}/vst3sdk"
URL="https://github.com/steinbergmedia/vst3sdk.git"
# Host build needs these only (skip doc / tutorials / vstgui4).
REQUIRED=(base cmake pluginterfaces public.sdk)

sdk_ready() {
  local root="$1"
  [[ -f "${root}/CMakeLists.txt" ]] || return 1
  local name
  for name in "${REQUIRED[@]}"; do
    [[ -d "${root}/${name}" ]] || return 1
  done
  return 0
}

if ! command -v git >/dev/null 2>&1; then
  echo "git is required on PATH." >&2
  exit 1
fi

if [[ -d "${DEST}" ]]; then
  if [[ "${FORCE}" -eq 0 ]] && sdk_ready "${DEST}"; then
    echo "VST3 SDK already present at ${DEST} (use --force to re-clone)."
    exit 0
  fi
  echo "Removing existing ${DEST} ..."
  rm -rf "${DEST}"
fi

echo "Cloning ${URL} -> ${DEST}"
git clone "${URL}" "${DEST}"
git -C "${DEST}" checkout --force "${COMMIT}"
git -C "${DEST}" submodule update --init -- "${REQUIRED[@]}"

if ! sdk_ready "${DEST}"; then
  echo "SDK clone finished but required folders are missing under ${DEST}." >&2
  exit 1
fi

echo "VST3 SDK ready at ${DEST} (commit ${COMMIT})."
echo "Next (Windows): ./Build.ps1 -Install"
echo "Next (macOS):   ../macos-vst-host/Build.sh --Install"
echo "Next (Linux):   ../linux-vst-host/Build.sh --Install"
