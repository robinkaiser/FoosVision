# SPDX-License-Identifier: GPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 Robin Kaiser

[CmdletBinding()]
param(
    [string]$RecorderDevice,
    [string]$ViewerDevice,
    [string]$RecorderDisplayName = "Recorder",
    [string]$ViewerDisplayName = "Viewer",
    [string]$Configuration = "Debug",
    [int]$RecorderStartupDelaySeconds = 2,
    [int]$PostStartCheckSeconds = 3,
    [string]$AdbPath,
    [switch]$SkipInstall,
    [switch]$CheckOnly,
    [switch]$StartOnly,
    [switch]$ViewerOnly,
    [switch]$RecorderOnly,
    [switch]$DisableFastDeployment,
    [switch]$NoEmbeddedFallback,
    [switch]$KeepLogcat
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$AppProject = Join-Path $RepoRoot "Product\Apps\FoosVision\FoosVision.csproj"
$DotnetHome = Join-Path $RepoRoot ".dotnet"
$AppPackage = "org.foosvision.app"
$RecorderLaunchActivity = "org.foosvision.app.RecorderLauncherActivity"
$ViewerLaunchActivity = "org.foosvision.app.ViewerLauncherActivity"

if ($ViewerOnly -and $RecorderOnly) {
    throw "Use either -ViewerOnly or -RecorderOnly, not both."
}

if ([string]::IsNullOrWhiteSpace($Configuration)) {
    $Configuration = "Debug"
}

if (-not $ViewerOnly -and [string]::IsNullOrWhiteSpace($RecorderDevice)) {
    throw "Pass -RecorderDevice for the recorder Android device serial."
}

if (-not $RecorderOnly -and [string]::IsNullOrWhiteSpace($ViewerDevice)) {
    throw "Pass -ViewerDevice for the viewer Android device serial."
}

function Resolve-AdbPath {
    param(
        [string]$RequestedAdbPath
    )

    if ($RequestedAdbPath) {
        if (-not (Test-Path -LiteralPath $RequestedAdbPath)) {
            throw "ADB was not found at '$RequestedAdbPath'."
        }

        return (Resolve-Path -LiteralPath $RequestedAdbPath).Path
    }

    $PathCommand = Get-Command "adb.exe" -ErrorAction SilentlyContinue
    if ($PathCommand) {
        return $PathCommand.Source
    }

    $VisualStudioAdbPath = "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe"
    if (Test-Path -LiteralPath $VisualStudioAdbPath) {
        return $VisualStudioAdbPath
    }

    throw "ADB was not found. Add Android platform-tools to PATH or pass -AdbPath."
}

function Get-AdbDevices {
    param(
        [string]$ResolvedAdbPath
    )

    & $ResolvedAdbPath devices -l |
        Select-Object -Skip 1 |
        Where-Object { $_.Trim().Length -gt 0 } |
        ForEach-Object {
            $Columns = $_ -split "\s+"
            [PSCustomObject]@{
                Serial = $Columns[0]
                State = $Columns[1]
                Line = $_
            }
        }
}

function Assert-DeviceReady {
    param(
        [object[]]$Devices,
        [string]$Serial,
        [string]$Role
    )

    $Device = $Devices | Where-Object { $_.Serial -eq $Serial } | Select-Object -First 1
    if (-not $Device) {
        $AvailableDevices = ($Devices | ForEach-Object { $_.Line }) -join [Environment]::NewLine
        throw "$Role device '$Serial' is not visible to ADB. Visible devices:$([Environment]::NewLine)$AvailableDevices"
    }

    if ($Device.State -ne "device") {
        throw "$Role device '$Serial' is visible but not ready. ADB state: '$($Device.State)'."
    }
}

function Invoke-DotnetAndroidTarget {
    param(
        [string]$Project,
        [string]$Target,
        [string]$DeviceSerial,
        [switch]$UseEmbeddedAssemblies
    )

    $Arguments = @(
        "build",
        $Project,
        "-f", "net10.0-android",
        "-c", $Configuration,
        "-t:$Target",
        "-p:AdbTarget=-s $DeviceSerial",
        "-v:minimal"
    )

    if ($UseEmbeddedAssemblies) {
        $Arguments += "-p:EmbedAssembliesIntoApk=true"
    }

    Write-Host "dotnet $($Arguments -join ' ')"

    $DotnetOutput = New-Object System.Collections.Generic.List[string]
    $PreviousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & dotnet @Arguments 2>&1 | ForEach-Object {
            $Line = $_.ToString()
            $DotnetOutput.Add($Line)
            Write-Host $Line
        }
        $ExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $PreviousErrorActionPreference
    }

    [PSCustomObject]@{
        Success = $ExitCode -eq 0
        ExitCode = $ExitCode
        Output = ($DotnetOutput -join [Environment]::NewLine)
    }
}

function Uninstall-AndroidPackage {
    param(
        [string]$DeviceSerial,
        [string]$PackageName,
        [string]$DisplayName
    )

    Write-Host "Uninstalling stale $DisplayName package from $DeviceSerial..."
    & $ResolvedAdbPath -s $DeviceSerial uninstall $PackageName | Out-Host
}

function Install-AndroidProject {
    param(
        [string]$Project,
        [string]$DeviceSerial,
        [string]$PackageName,
        [string]$DisplayName
    )

    Write-Host "Installing $DisplayName..."
    $Result = Invoke-DotnetAndroidTarget -Project $Project -Target "Install" -DeviceSerial $DeviceSerial -UseEmbeddedAssemblies:$DisableFastDeployment
    if ($Result.Success) {
        return
    }

    if ($DisableFastDeployment) {
        throw "Install failed for $DisplayName on device '$DeviceSerial'."
    }

    Write-Warning "Fast deployment failed for $DisplayName. Retrying once after uninstalling the existing package."
    Uninstall-AndroidPackage -DeviceSerial $DeviceSerial -PackageName $PackageName -DisplayName $DisplayName

    $RetryResult = Invoke-DotnetAndroidTarget -Project $Project -Target "Install" -DeviceSerial $DeviceSerial
    if ($RetryResult.Success) {
        return
    }

    if ($NoEmbeddedFallback) {
        throw "Fast deployment retry failed for $DisplayName on device '$DeviceSerial'."
    }

    Write-Warning "Fast deployment retry failed for $DisplayName. Falling back to EmbedAssembliesIntoApk=true."
    $EmbeddedResult = Invoke-DotnetAndroidTarget -Project $Project -Target "Install" -DeviceSerial $DeviceSerial -UseEmbeddedAssemblies
    if (-not $EmbeddedResult.Success) {
        throw "Embedded APK install failed for $DisplayName on device '$DeviceSerial'."
    }
}

function Start-AndroidPackage {
    param(
        [string]$DeviceSerial,
        [string]$PackageName,
        [string]$LaunchActivity,
        [string]$DisplayName
    )

    $Activity = "$PackageName/$LaunchActivity"
    Write-Host "Starting $DisplayName..."
    & $ResolvedAdbPath -s $DeviceSerial shell am start -S -a android.intent.action.MAIN -c android.intent.category.LAUNCHER -n $Activity | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to start activity '$Activity' on device '$DeviceSerial'."
    }
}

function Get-AndroidPackagePid {
    param(
        [string]$DeviceSerial,
        [string]$PackageName
    )

    $PidOutput = & $ResolvedAdbPath -s $DeviceSerial shell pidof $PackageName 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    $PidOutput.Trim()
}

function Write-AndroidCrashLog {
    param(
        [string]$DeviceSerial,
        [string]$PackageName
    )

    Write-Host ""
    Write-Host "Recent Android crash log for $PackageName on ${DeviceSerial}:"
    & $ResolvedAdbPath -s $DeviceSerial logcat -d -v time -t 1000 |
        Select-String -Pattern $PackageName, "AndroidRuntime", "FATAL EXCEPTION", "Unhandled", "Exception", "FoosVision" |
        ForEach-Object { Write-Host $_.Line }
}

if (-not (Test-Path -LiteralPath $DotnetHome)) {
    New-Item -ItemType Directory -Path $DotnetHome | Out-Null
}

$env:DOTNET_CLI_HOME = $DotnetHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$ResolvedAdbPath = Resolve-AdbPath -RequestedAdbPath $AdbPath
$Devices = @(Get-AdbDevices -ResolvedAdbPath $ResolvedAdbPath)

if (-not $ViewerOnly) {
    Assert-DeviceReady -Devices $Devices -Serial $RecorderDevice -Role "Recorder"
}

if (-not $RecorderOnly) {
    Assert-DeviceReady -Devices $Devices -Serial $ViewerDevice -Role "Viewer"
}

if (-not $ViewerOnly) {
    Write-Host "Recorder device: $RecorderDevice"
}

if (-not $RecorderOnly) {
    Write-Host "Viewer device:   $ViewerDevice"
}

Write-Host ""

if ($CheckOnly) {
    Write-Host "Both Android devices are visible and ready."
    exit 0
}

if ($SkipInstall) {
    $StartOnly = $true
}

if (-not $StartOnly) {
    if (-not $ViewerOnly) {
        Install-AndroidProject -Project $AppProject -DeviceSerial $RecorderDevice -PackageName $AppPackage -DisplayName $RecorderDisplayName
    }

    if (-not $RecorderOnly) {
        Install-AndroidProject -Project $AppProject -DeviceSerial $ViewerDevice -PackageName $AppPackage -DisplayName $ViewerDisplayName
    }
}

if (-not $ViewerOnly) {
    Start-AndroidPackage -DeviceSerial $RecorderDevice -PackageName $AppPackage -LaunchActivity $RecorderLaunchActivity -DisplayName $RecorderDisplayName

    if ($RecorderStartupDelaySeconds -gt 0) {
        Write-Host "Waiting $RecorderStartupDelaySeconds second(s) before starting Viewer..."
        Start-Sleep -Seconds $RecorderStartupDelaySeconds
    }
}

if ($RecorderOnly) {
    Write-Host "Recorder was started."
    exit 0
}

if (-not $KeepLogcat) {
    & $ResolvedAdbPath -s $ViewerDevice logcat -c
}

Start-AndroidPackage -DeviceSerial $ViewerDevice -PackageName $AppPackage -LaunchActivity $ViewerLaunchActivity -DisplayName $ViewerDisplayName

if ($PostStartCheckSeconds -gt 0) {
    Start-Sleep -Seconds $PostStartCheckSeconds
    $ViewerPid = Get-AndroidPackagePid -DeviceSerial $ViewerDevice -PackageName $AppPackage
    if (-not $ViewerPid) {
        Write-AndroidCrashLog -DeviceSerial $ViewerDevice -PackageName $AppPackage
        throw "Viewer process is not running after start."
    }

    Write-Host "Viewer is running with PID $ViewerPid."
}

if ($ViewerOnly) {
    Write-Host "Viewer was started."
}
else {
    Write-Host "Recorder and Viewer were started."
}
