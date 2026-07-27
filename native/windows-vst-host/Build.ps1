<#
.SYNOPSIS
    Build VstHostNative.dll (x64 and/or ARM64 Release) and optionally install to Plugins/.
.PARAMETER Configuration
    CMake build type. Default: Release
.PARAMETER Platform
    Target architecture: x64, ARM64, or Both. Default: Both
.PARAMETER Install
    Copy DLL(s) to Plugins/Windows/<arch>/ after build.
#>
param(
    [ValidateSet("Debug", "Release", "RelWithDebInfo")]
    [string]$Configuration = "Release",
    [ValidateSet("x64", "ARM64", "Both")]
    [string]$Platform = "Both",
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

function Get-PluginSubdir([string]$Arch) {
    if ($Arch -eq "ARM64") { return "ARM64" }
    return "x86_64"
}

function Get-CmakeArch([string]$Arch) {
    if ($Arch -eq "ARM64") { return "ARM64" }
    return "x64"
}

function Build-One([string]$Arch) {
    $CmakeArch = Get-CmakeArch $Arch
    $PluginSubdir = Get-PluginSubdir $Arch
    $BuildDir = Join-Path $ScriptDir ("build-" + $Arch.ToLower())

    Write-Host "`n=== VstHostNative Build ($Arch) ===" -ForegroundColor Cyan
    Write-Host "Configuration : $Configuration"
    Write-Host "Build Dir     : $BuildDir"

    if (!(Test-Path $BuildDir)) { New-Item -ItemType Directory -Path $BuildDir | Out-Null }
    cmake -S $ScriptDir -B $BuildDir -A $CmakeArch `
        -DCMAKE_BUILD_TYPE=$Configuration
    if ($LASTEXITCODE -ne 0) { throw "CMake configure failed ($Arch)." }

    cmake --build $BuildDir --config $Configuration --parallel
    if ($LASTEXITCODE -ne 0) { throw "CMake build failed ($Arch)." }

    $Dll = Join-Path $BuildDir "bin/$Configuration/VstHostNative.dll"
    if (!(Test-Path $Dll)) {
        throw "DLL not found at expected path: $Dll"
    }
    Write-Host "Build succeeded: $Dll" -ForegroundColor Green

    if ($Install) {
        $PluginDir = Join-Path $ScriptDir "../../Plugins/Windows/$PluginSubdir"
        if (!(Test-Path $PluginDir)) { New-Item -ItemType Directory -Path $PluginDir -Force | Out-Null }
        $Dest = Join-Path $PluginDir "VstHostNative.dll"
        try {
            Copy-Item $Dll -Destination $Dest -Force
            Write-Host "Installed to: $PluginDir" -ForegroundColor Green
        } catch {
            Write-Host "WARNING: could not overwrite $Dest (file locked?). Built DLL remains at: $Dll" -ForegroundColor Yellow
            Write-Host $_.Exception.Message -ForegroundColor Yellow
        }
    }

    return $Dll
}

$arches = if ($Platform -eq "Both") { @("x64", "ARM64") } else { @($Platform) }
foreach ($arch in $arches) {
    Build-One $arch | Out-Null
}

Write-Host "`nAll requested architectures built." -ForegroundColor Green
