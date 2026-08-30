# Diagnostics

FoosVision writes structured diagnostics artifacts below a `Diagnostics` directory. The active diagnostics path is logged during startup. If Seq is enabled, the path is also visible in Seq as `DiagnosticsPath`.

## Typical Locations

Android apps prefer the app-specific external files directory:

```text
/storage/emulated/0/Android/data/org.foosvision.app/files/FoosVision.Recorder/Diagnostics/
/storage/emulated/0/Android/data/org.foosvision.app/files/FoosVision.Viewer/Diagnostics/
```

Typical contents:

```text
Diagnostics/
  Logs/
  Videos/
```

Notes:

- `Recorder` writes file logs to `Logs/` and can write diagnostic video artifacts to `Videos/`.
- Diagnostic video artifacts are controlled by `diagnostics.video`. When enabled, the recorder writes them for both stopped install sessions and stopped game sessions.
- `Viewer` writes file logs to `Logs/`.
- `Viewer` starts file logging immediately at app startup, so discovery and handshake problems are captured locally.
- `Viewer` enables Seq only after a successful recorder handshake, using the recorder-provided diagnostics settings.
- Runtime performance findings are collected in [Performance Notes](Development/Performance.md).

## Seq Setup

Install Seq from the [Datalust download page](https://datalust.co/download). Datalust provides a Windows MSI and an official Docker/Linux image. The Docker image can run on hosts that support Linux containers, for example on Linux servers or through Docker Desktop.

The important requirement is that the recorder and viewer Android devices can reach the configured Seq HTTP URL.

Enable Seq in the recorder `Config.json`; the viewer receives the same Seq settings during the recorder handshake.
Use the IP address or host name that the Android devices can reach, not necessarily `localhost` on the machine running Seq.
The full configuration schema is documented in [Configuration](Configuration.md).

### Windows Firewall Setup

If Seq runs on Windows, the Windows machine must allow incoming connections on TCP port `5341`.

Start PowerShell or `cmd` as Administrator and run:

```powershell
netsh advfirewall firewall add rule name="FoosVision Seq 5341" dir=in action=allow protocol=TCP localport=5341
```

To verify the rule:

```powershell
netsh advfirewall firewall show rule name="FoosVision Seq 5341"
```

Important notes:

- The firewall command must be executed with Administrator privileges.
- On Android, test the Seq URL in the browser with an explicit `http://` prefix, for example `http://192.168.1.6:5341`.
- If only `192.168.1.6:5341` is entered, the browser may force HTTPS and fail with `ERR_SSL_PROTOCOL_ERROR`.
- Useful Seq filters are `App = 'Recorder'`, `App = 'Viewer'`, `App = 'VideoPlayer'`, or `DiagnosticsPath like '%Diagnostics%'`.

## Pulling Android Logs

Use `adb pull` when logs need to be inspected or imported after the fact:

### ADB Setup

Install Android SDK Platform-Tools from the [Android Developers download page](https://developer.android.com/studio/releases/platform-tools). If Android Studio is installed, use the Platform-Tools copy installed with the Android SDK instead.

Typical Windows SDK location:

```text
C:\Program Files (x86)\Android\android-sdk\platform-tools
```

List connected devices:

```powershell
.\adb.exe devices
```

Open an ADB shell:

```powershell
.\adb.exe shell
```

### Pull Logs

```powershell
adb pull /storage/emulated/0/Android/data/org.foosvision.app/files/FoosVision.Recorder/Diagnostics ./RecorderDiagnostics
adb pull /storage/emulated/0/Android/data/org.foosvision.app/files/FoosVision.Viewer/Diagnostics ./ViewerDiagnostics
```

If the app fell back to internal app data, use the `DiagnosticsPath` value from the startup log or Seq event to locate the actual directory.

## Importing File Logs Into Seq

Log files are written as newline-delimited compact JSON events, so they can be imported into Seq.

Install and configure `seqcli`:

```powershell
dotnet tool install --global seqcli
seqcli config -k connection.serverUrl -v http://192.168.1.6:5341
```

Import one file:

```powershell
seqcli ingest -i ".\ViewerDiagnostics\Logs\viewer-20260610.log" --json
```

When importing with `--json`, Seq keeps the original event timestamps from the log file. If imported events do not appear immediately, expand the Seq time range, for example to `Today`, `Last 24 hours`, or a custom range around the log file timestamps.

Import all pulled FoosVision logs:

```powershell
Get-ChildItem ".\ViewerDiagnostics\Logs\*.log", ".\RecorderDiagnostics\Logs\*.log" | ForEach-Object {
    seqcli ingest -i $_.FullName --json
}
```
