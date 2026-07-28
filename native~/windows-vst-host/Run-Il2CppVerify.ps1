<#
.SYNOPSIS
    Create a temp Unity project, embed this package (without native~/), run IL2CPP Win64 verify.
#>
param(
    [string]$UnityEditor = "D:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe",
    [string]$PackageRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$ProjectRoot = (Join-Path $env:TEMP "Unity-VST3-Bridge-Verify"),
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

if (!(Test-Path $UnityEditor)) {
    throw "Unity Editor not found: $UnityEditor"
}

Write-Host "Package: $PackageRoot"
Write-Host "Project: $ProjectRoot"
Write-Host "Unity:   $UnityEditor"

if (Test-Path $ProjectRoot) {
    Remove-Item -Recurse -Force $ProjectRoot
}

$createLog = Join-Path $env:TEMP "Unity-VST3-Bridge-create.log"
Write-Host "Creating Unity project..."
$create = Start-Process -FilePath $UnityEditor -ArgumentList @(
    "-batchmode", "-nographics", "-quit",
    "-createProject", $ProjectRoot,
    "-logFile", $createLog
) -Wait -PassThru
if ($create.ExitCode -ne 0) {
    if (Test-Path $createLog) { Get-Content $createLog -Tail 40 }
    throw "createProject failed: $($create.ExitCode)"
}

# Embed UPM-visible package subset (exclude native~/ build tree and Samples~).
$embedded = Join-Path $ProjectRoot "Packages\jp.kshoji.unity.vst3nativehost"
New-Item -ItemType Directory -Force -Path $embedded | Out-Null
$copyItems = @(
    "package.json",
    "README.md",
    "CHANGELOG.md",
    "LICENSE",
    "NOTICE.md",
    "Runtime",
    "Editor",
    "Plugins",
    "Documentation~"
)
foreach ($item in $copyItems) {
    $src = Join-Path $PackageRoot $item
    if (!(Test-Path $src)) { continue }
    $dst = Join-Path $embedded $item
    if (Test-Path $src -PathType Container) {
        Copy-Item -Recurse -Force $src $dst
    } else {
        Copy-Item -Force $src $dst
    }
}

# Sample into Assets (Samples~ is not auto-imported)
$sampleSrc = Join-Path $PackageRoot "Samples~\VstHostSample"
$sampleDst = Join-Path $ProjectRoot "Assets\Samples\VstHostSample"
New-Item -ItemType Directory -Force -Path (Split-Path $sampleDst -Parent) | Out-Null
Copy-Item -Recurse -Force $sampleSrc $sampleDst

# Scene GUID must match Samples~/.../VstHostSampleScene.unity.meta
$ebs = Join-Path $ProjectRoot "ProjectSettings\EditorBuildSettings.asset"
@"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1045 &1
EditorBuildSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Scenes:
  - enabled: 1
    path: Assets/Samples/VstHostSample/Scenes/VstHostSampleScene.unity
    guid: c3d4e5f6789012345678abcdef012345
  m_configObjects: {}
"@ | Set-Content -Encoding Ascii $ebs

if ($SkipBuild) {
    Write-Host "Project prepared (SkipBuild)."
    exit 0
}

$log = Join-Path $ProjectRoot "il2cpp-verify.log"
Write-Host "Running Unity batchmode IL2CPP verify (several minutes)..."
$build = Start-Process -FilePath $UnityEditor -ArgumentList @(
    "-batchmode", "-nographics", "-quit",
    "-projectPath", $ProjectRoot,
    "-executeMethod", "jp.kshoji.unity.vst3nativehost.Editor.VstHostBuildVerify.BuildIl2CppWin64",
    "-logFile", $log
) -Wait -PassThru

Write-Host "Unity exit code: $($build.ExitCode)"
Write-Host "Log: $log"
if ($build.ExitCode -ne 0) {
    if (Test-Path $log) {
        Select-String -Path $log -Pattern "error CS|Build failed|VstHost|Exception:|DisplayProgressNotification" |
            Select-Object -Last 40 |
            ForEach-Object { $_.Line }
    }
    exit $build.ExitCode
}

& (Join-Path $PSScriptRoot "Verify-MidiIsolation.ps1") -MidiRepoRoot "C:\Users\0x0ba\Documents\github\Unity-MIDI-Plugin"

Write-Host "OK: IL2CPP Win64 verify build succeeded." -ForegroundColor Green
exit 0
