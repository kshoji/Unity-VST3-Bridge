<#
.SYNOPSIS
    Clone the Steinberg VST3 SDK into ./vst3sdk for native rebuilds (developers only).
.DESCRIPTION
    UPM Git URL installs must not recurse into this tree (Windows path-length failures).
    End users use prebuilt Plugins/; rebuilders run this script once, then Build.ps1.
.PARAMETER Commit
    vst3sdk commit to check out. Default matches the former submodule pin.
.PARAMETER Force
    Delete an existing ./vst3sdk and re-clone.
#>
param(
    [string]$Commit = "58f8da7936800732561402d7936584ca4505de07",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$Dest = Join-Path $ScriptDir "vst3sdk"
$Url = "https://github.com/steinbergmedia/vst3sdk.git"
# Host build needs these only (skip doc / tutorials / vstgui4 — long paths on Windows).
$Required = @("base", "cmake", "pluginterfaces", "public.sdk")

function Test-SdkReady([string]$Root) {
    foreach ($name in $Required) {
        $marker = Join-Path $Root $name
        if (!(Test-Path $marker)) { return $false }
    }
    return (Test-Path (Join-Path $Root "CMakeLists.txt"))
}

if (!(Get-Command git -ErrorAction SilentlyContinue)) {
    throw "git is required on PATH."
}

# Help Git check out long paths under public.sdk/samples on Windows.
try { git config --global core.longpaths true 2>$null } catch { }

if (Test-Path $Dest) {
    if (-not $Force -and (Test-SdkReady $Dest)) {
        Write-Host "VST3 SDK already present at $Dest (use -Force to re-clone)." -ForegroundColor Green
        exit 0
    }
    Write-Host "Removing existing $Dest ..." -ForegroundColor Yellow
    Remove-Item -LiteralPath $Dest -Recurse -Force
}

Write-Host "Cloning $Url -> $Dest" -ForegroundColor Cyan
git clone $Url $Dest
if ($LASTEXITCODE -ne 0) { throw "git clone failed." }

Push-Location $Dest
try {
    git checkout --force $Commit
    if ($LASTEXITCODE -ne 0) { throw "git checkout $Commit failed." }

    git submodule update --init -- @Required
    if ($LASTEXITCODE -ne 0) { throw "git submodule update failed." }
}
finally {
    Pop-Location
}

if (!(Test-SdkReady $Dest)) {
    throw "SDK clone finished but required folders are missing under $Dest."
}

Write-Host "VST3 SDK ready at $Dest (commit $Commit)." -ForegroundColor Green
Write-Host "Next: .\Build.ps1 -Install"
