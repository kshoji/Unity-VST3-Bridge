#!/usr/bin/env bash
# Verify Unity-MIDI-Plugin does not contain VST host artifacts (macOS counterpart
# of Verify-MidiIsolation.ps1).
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <Unity-MIDI-Plugin-repo-root>"
  exit 2
fi

MIDI_ROOT="$1"
if [[ ! -d "${MIDI_ROOT}" ]]; then
  echo "MIDI repo not found: ${MIDI_ROOT}"
  exit 2
fi

failures=0
fail() {
  echo "FAIL: $*"
  failures=$((failures + 1))
}

echo "=== MIDI isolation check (macOS) ==="
echo "MIDI root: ${MIDI_ROOT}"

FORBIDDEN_NAMES=(
  "VstHostNative.dll"
  "VstHostNative.pdb"
  "VstHostNative.bundle"
  "VstHostNative.dylib"
  "jp.kshoji.unity.vst3nativehost"
)

search_roots=()
for d in Assets native Packages; do
  if [[ -d "${MIDI_ROOT}/${d}" ]]; then
    search_roots+=("${MIDI_ROOT}/${d}")
  fi
done

for root in "${search_roots[@]}"; do
  while IFS= read -r -d '' file; do
    name="$(basename "${file}")"
    # Skip markdown plan/docs mentions
    if [[ "${file}" == *.md ]]; then
      continue
    fi
    for pat in "${FORBIDDEN_NAMES[@]}"; do
      if [[ "${name}" == *"${pat}"* ]] || [[ "${file}" == *"${pat}"* ]]; then
        fail "Forbidden artifact: ${file}"
      fi
    done
    if [[ "${name}" == "vst3sdk" ]] || [[ "${file}" == */vst3sdk ]] || [[ "${file}" == */vst3sdk/* ]]; then
      fail "VST3 SDK path under MIDI repo: ${file}"
    fi
  done < <(find "${root}" -type f -print0 2>/dev/null)

  while IFS= read -r -d '' dir; do
    name="$(basename "${dir}")"
    if [[ "${name}" == "vst3sdk" ]]; then
      fail "VST3 SDK directory under MIDI repo: ${dir}"
    fi
    if [[ "${dir}" == *"/Assets/MIDI/"*"/VstHost"* ]]; then
      fail "VST host folder under Assets/MIDI: ${dir}"
    fi
  done < <(find "${root}" -type d -print0 2>/dev/null)
done

manifest="${MIDI_ROOT}/Packages/manifest.json"
if [[ -f "${manifest}" ]] && grep -q 'jp\.kshoji\.unity\.vst3nativehost' "${manifest}"; then
  echo "NOTE: manifest references VST package (OK for local Verification B; remove for MIDI-only release)."
fi

if [[ "${failures}" -eq 0 ]]; then
  echo "OK: no VST host binaries/SDK/implementation under MIDI Assets/native."
  exit 0
fi

echo "${failures} isolation failure(s)"
exit 1
