<#
.SYNOPSIS
    Build VstHostNative.dll (x64 Release) and optionally install to Plugins/.
.PARAMETER Configuration
    CMake build type. Default: Release
.PARAMETER Install
    Copy DLL to Plugins/Windows/x86_64/ after build.
#>
param(
    [ValidateSet("Debug", "Release", "RelWithDebInfo")]
    [string]$Configuration = "Release",
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$BuildDir  = Join-Path $ScriptDir "build"

Write-Host "=== VstHostNative Build ===" -ForegroundColor Cyan
Write-Host "Configuration : $Configuration"
Write-Host "Build Dir     : $BuildDir"

# Configure
if (!(Test-Path $BuildDir)) { New-Item -ItemType Directory -Path $BuildDir | Out-Null }
cmake -S $ScriptDir -B $BuildDir -A x64 `
    -DCMAKE_BUILD_TYPE=$Configuration
if ($LASTEXITCODE -ne 0) { throw "CMake configure failed." }

# Build
cmake --build $BuildDir --config $Configuration --parallel
if ($LASTEXITCODE -ne 0) { throw "CMake build failed." }

$Dll = Join-Path $BuildDir "bin/$Configuration/VstHostNative.dll"
if (Test-Path $Dll) {
    Write-Host "`nBuild succeeded: $Dll" -ForegroundColor Green
} else {
    throw "DLL not found at expected path: $Dll"
}

# Install
if ($Install) {
    $PluginDir = Join-Path $ScriptDir "../../Plugins/Windows/x86_64"
    if (!(Test-Path $PluginDir)) { New-Item -ItemType Directory -Path $PluginDir -Force | Out-Null }
    Copy-Item $Dll -Destination $PluginDir -Force
    Write-Host "Installed to: $PluginDir" -ForegroundColor Green
}
