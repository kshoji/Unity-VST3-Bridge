<#
.SYNOPSIS
    Verify Unity-MIDI-Plugin does not contain VST host artifacts.
.PARAMETER MidiRepoRoot
    Path to Unity-MIDI-Plugin repository root.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$MidiRepoRoot
)

$ErrorActionPreference = "Stop"

if (!(Test-Path $MidiRepoRoot)) {
    throw "MIDI repo not found: $MidiRepoRoot"
}

$failures = New-Object System.Collections.Generic.List[string]

function Add-Fail([string]$message) {
    $failures.Add($message) | Out-Null
    Write-Host "FAIL: $message" -ForegroundColor Red
}

Write-Host "=== MIDI isolation check ===" -ForegroundColor Cyan
Write-Host "MIDI root: $MidiRepoRoot"

# Forbidden binaries / SDK trees under MIDI Assets or native
$forbiddenNamePatterns = @(
    "VstHostNative.dll",
    "VstHostNative.pdb",
    "VstHostNative.bundle",
    "VstHostNative.dylib",
    "jp.kshoji.unity.vst3nativehost"
)

$searchRoots = @(
    (Join-Path $MidiRepoRoot "Assets"),
    (Join-Path $MidiRepoRoot "native"),
    (Join-Path $MidiRepoRoot "Packages")
) | Where-Object { Test-Path $_ }

foreach ($root in $searchRoots) {
    Get-ChildItem -Path $root -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
        $name = $_.Name
        $full = $_.FullName
        foreach ($pat in $forbiddenNamePatterns) {
            if ($name -like "*$pat*" -or $full -like "*$pat*") {
                # Allow documentation mentions of the package name only in .md under documents
                if ($_.Extension -eq ".md") {
                    continue
                }
                Add-Fail "Forbidden artifact: $full"
            }
        }
        if ($name -eq "vst3sdk" -or $full -match "[\\/]vst3sdk([\\/]|$)") {
            Add-Fail "VST3 SDK path under MIDI repo: $full"
        }
    }

    Get-ChildItem -Path $root -Recurse -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.Name -eq "vst3sdk") {
            Add-Fail "VST3 SDK directory under MIDI repo: $($_.FullName)"
        }
        if ($_.FullName -match "[\\/]Assets[\\/]MIDI[\\/].*[\\/]VstHost([\\/]|$)") {
            Add-Fail "VST host folder under Assets/MIDI: $($_.FullName)"
        }
    }
}

# manifest must not depend on VST package for MIDI-only shipping baseline
$manifest = Join-Path $MidiRepoRoot "Packages\manifest.json"
if (Test-Path $manifest) {
    $text = Get-Content $manifest -Raw
    if ($text -match "jp\.kshoji\.unity\.vst3nativehost") {
        Write-Host "NOTE: manifest references VST package (OK for local MIDI+VST verify; remove for MIDI-only release)." -ForegroundColor Yellow
    }
}

if ($failures.Count -eq 0) {
    Write-Host "OK: no VST host binaries/SDK/implementation under MIDI Assets/native." -ForegroundColor Green
    exit 0
}

Write-Host ("{0} isolation failure(s)" -f $failures.Count) -ForegroundColor Red
exit 1
