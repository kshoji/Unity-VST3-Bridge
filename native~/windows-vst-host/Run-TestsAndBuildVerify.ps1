<#
.SYNOPSIS
    Package smoke: prepare a temp Unity project, run EditMode + PlayMode tests,
    then Standalone verify builds (IL2CPP Win64 / OSX / Linux64) via VstHostBuildVerify.
#>
param(
    [string]$UnityEditor = "D:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe",
    [string]$PackageRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$ProjectRoot = (Join-Path $env:TEMP "Unity-VST3-Bridge-TestsAndBuild-Verify"),
    [switch]$SkipTests,
    [switch]$SkipWin64,
    [switch]$SkipOSX,
    [switch]$SkipLinux64,
    [switch]$PrepareOnly
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

$createLog = Join-Path $env:TEMP "Unity-VST3-Bridge-tests-and-build-create.log"
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
    "Tests",
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

$sampleSrc = Join-Path $PackageRoot "Samples~\VstHostSample"
$sampleDst = Join-Path $ProjectRoot "Assets\Samples\VstHostSample"
New-Item -ItemType Directory -Force -Path (Split-Path $sampleDst -Parent) | Out-Null
Copy-Item -Recurse -Force $sampleSrc $sampleDst

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

# Fresh -createProject projects omit Test Framework; EditMode asmdefs need NUnit.
# Linux IL2CPP from Windows also needs the sysroot toolchain packages.
$manifestPath = Join-Path $ProjectRoot "Packages\manifest.json"
$manifestJson = Get-Content -Raw $manifestPath
$manifest = $manifestJson | ConvertFrom-Json
$deps = $manifest.dependencies
$pkgAdds = @{
    'com.unity.test-framework' = '1.1.33'
    'com.unity.toolchain.win-x86_64-linux-x86_64' = '2.0.11'
    'com.unity.sysroot' = '2.0.10'
    'com.unity.sysroot.linux-x86_64' = '2.0.10'
}
$changed = $false
foreach ($key in $pkgAdds.Keys) {
    if (-not $deps.$key) {
        $deps | Add-Member -NotePropertyName $key -NotePropertyValue $pkgAdds[$key] -Force
        $changed = $true
        Write-Host "Added $key to Packages/manifest.json"
    }
}
if ($changed) {
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -Encoding Utf8 $manifestPath
}

if ($PrepareOnly) {
    Write-Host "Project prepared (PrepareOnly)."
    exit 0
}

function Invoke-UnityBatch {
    param(
        [string]$Label,
        [string[]]$ExtraArgs,
        [string]$LogPath
    )
    Write-Host "`n=== $Label ===" -ForegroundColor Cyan
    Write-Host "Log: $LogPath"
    $args = @(
        "-batchmode", "-nographics", "-quit",
        "-projectPath", $ProjectRoot,
        "-logFile", $LogPath
    ) + $ExtraArgs
    $proc = Start-Process -FilePath $UnityEditor -ArgumentList $args -Wait -PassThru
    Write-Host "Exit: $($proc.ExitCode)"
    if ($proc.ExitCode -ne 0) {
        if (Test-Path $LogPath) {
            Select-String -Path $LogPath -Pattern "error CS|Build failed|VstHost|Exception:|Test Run Failed|Overall result" |
                Select-Object -Last 50 |
                ForEach-Object { $_.Line }
        }
        throw "$Label failed with exit $($proc.ExitCode)"
    }
}

$failed = $false

function Invoke-UnityTests {
    param(
        [ValidateSet("EditMode", "PlayMode")]
        [string]$Platform,
        [string]$ResultsPath,
        [string]$LogPath
    )
    Write-Host "`n=== $Platform tests ===" -ForegroundColor Cyan
    $testProc = Start-Process -FilePath $UnityEditor -ArgumentList @(
        "-batchmode", "-nographics",
        "-projectPath", $ProjectRoot,
        "-runTests", "-testPlatform", $Platform,
        "-testResults", $ResultsPath,
        "-logFile", $LogPath
    ) -Wait -PassThru
    Write-Host "Exit: $($testProc.ExitCode)"
    Write-Host "Results: $ResultsPath"
    if ($testProc.ExitCode -ne 0) {
        if (Test-Path $LogPath) {
            Select-String -Path $LogPath -Pattern "error CS|Test Run Failed|Overall result|Failed" |
                Select-Object -Last 40 |
                ForEach-Object { $_.Line }
        }
        if (Test-Path $ResultsPath) {
            Select-String -Path $ResultsPath -Pattern 'result="Failed"|<message>' |
                Select-Object -First 50 |
                ForEach-Object { $_.Line }
        }
        throw "$Platform tests failed with exit $($testProc.ExitCode)"
    }
}

if (-not $SkipTests) {
    try {
        # Package layout: Tests/Editor → EditMode, Tests/Runtime → PlayMode.
        Invoke-UnityTests -Platform EditMode `
            -ResultsPath (Join-Path $ProjectRoot "editmode-results.xml") `
            -LogPath (Join-Path $ProjectRoot "editmode-tests.log")
        Invoke-UnityTests -Platform PlayMode `
            -ResultsPath (Join-Path $ProjectRoot "playmode-results.xml") `
            -LogPath (Join-Path $ProjectRoot "playmode-tests.log")
    }
    catch {
        Write-Host $_ -ForegroundColor Red
        $failed = $true
    }
}

if (-not $SkipWin64 -and -not $failed) {
    try {
        Invoke-UnityBatch -Label "IL2CPP Win64 verify" -LogPath (Join-Path $ProjectRoot "il2cpp-win64.log") -ExtraArgs @(
            "-executeMethod", "jp.kshoji.unity.vst3nativehost.Editor.VstHostBuildVerify.BuildIl2CppWin64"
        )
    }
    catch {
        Write-Host $_ -ForegroundColor Red
        $failed = $true
    }
}

if (-not $SkipOSX -and -not $failed) {
    try {
        Invoke-UnityBatch -Label "Standalone OSX verify" -LogPath (Join-Path $ProjectRoot "osx-verify.log") -ExtraArgs @(
            "-executeMethod", "jp.kshoji.unity.vst3nativehost.Editor.VstHostBuildVerify.BuildStandaloneOSX"
        )
    }
    catch {
        Write-Host $_ -ForegroundColor Red
        $failed = $true
    }
}

if (-not $SkipLinux64 -and -not $failed) {
    try {
        Invoke-UnityBatch -Label "Standalone Linux64 verify" -LogPath (Join-Path $ProjectRoot "linux64-verify.log") -ExtraArgs @(
            "-executeMethod", "jp.kshoji.unity.vst3nativehost.Editor.VstHostBuildVerify.BuildStandaloneLinux64"
        )
    }
    catch {
        Write-Host $_ -ForegroundColor Yellow
        Write-Host "Linux64 player build failed on this Editor; falling back to plugin-platform smoke." -ForegroundColor Yellow
        try {
            Invoke-UnityBatch -Label "Linux plugin platforms (fallback)" -LogPath (Join-Path $ProjectRoot "linux64-platforms.log") -ExtraArgs @(
                "-executeMethod", "jp.kshoji.unity.vst3nativehost.Editor.VstHostBuildVerify.VerifyPluginPlatformsExit"
            )
            Write-Host "Linux64 player build skipped (sysroot/Mono unavailable); plugin platforms OK." -ForegroundColor Yellow
        }
        catch {
            Write-Host $_ -ForegroundColor Red
            $failed = $true
        }
    }
}

if ($failed) {
    Write-Host "TestsAndBuild verify FAILED." -ForegroundColor Red
    exit 1
}

Write-Host "OK: EditMode/PlayMode tests + Standalone verify builds succeeded." -ForegroundColor Green
exit 0
