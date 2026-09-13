# Discovery Notes

This document describes the current recorder-viewer discovery strategy. The strategy is intentionally pragmatic because Android UDP broadcast delivery has proven unreliable across devices, routers, and internet/no-internet WLAN setups.

## Current Strategy

FoosVision currently uses one primary discovery path and one fallback path:

- UDP recorder announcements are the preferred path.
- Local TCP handshake probing is the viewer-side fallback when UDP announcements do not arrive.

Both paths converge on the same recorder handshake. A device is treated as a usable recorder only after it accepts the FoosVision handshake on TCP port `5555`.

## Ports

- UDP `5560`: recorder discovery announcements.
- TCP `5555`: recorder-viewer handshake.
- UDP `5561`: RTP/H.264 live video from recorder to viewer.

Other recorder channels are not part of discovery. The recorder binds them at startup, and the viewer connects to them only after a successful handshake.

## Discovery Sequence

```mermaid
sequenceDiagram
autonumber off

participant ViewerPage as VIEWER<br/>ViewerSessionController<br/>AttachRuntimeAsync()
participant AndroidLifecycle as VIEWER<br/>MainActivity<br/>OnResume()/OnPause()
participant ViewerRoot as VIEWER<br/>ViewerCompositionRoot<br/>ConnectAsync()
participant ViewNet as VIEWER<br/>NetworkModule<br/>ConnectAsync()
participant Conn as VIEWER<br/>RecorderConnectionService<br/>ConnectAsync()
participant UdpDisc as VIEWER<br/>UdpRecorderDiscoverySession
participant Fallback as VIEWER<br/>AndroidRecorderFallbackCandidateSource
participant HClient as VIEWER<br/>HandshakeClient<br/>HelloAsync()

participant RecorderApp as RECORDER<br/>RecorderHost<br/>Start()
participant RecNet as RECORDER<br/>NetworkModule
participant UdpServer as RECORDER<br/>UdpServer
participant HServer as RECORDER<br/>HandshakeServerHost
participant HHandler as RECORDER<br/>HandshakeHandler<br/>Handle()
participant Camera as RECORDER<br/>CameraController<br/>ConfigureUdpVideoStream()

Note over ViewerPage,HClient: Viewer side
Note over RecorderApp,Camera: Recorder side
Note over ViewerPage,Camera: Number ranges: 100 = UDP discovery, 200 = TCP connect probe fallback, 300 = FoosVision handshake

RecorderApp->>RecNet: Start()
RecNet->>RecNet: StartDiscovery()
RecNet->>UdpServer: Start()
RecNet->>HServer: Start("tcp://*:5555")

ViewerPage->>ViewerRoot: ConnectAsync(ct)
AndroidLifecycle->>Fallback: Enable TCP fallback probing<br/>while viewer is foreground
ViewerRoot->>ViewNet: ConnectAsync(ct)
ViewNet->>Conn: ConnectAsync(ct)

autonumber 100 1
Conn->>UdpDisc: Start UDP discovery session
Conn->>Conn: Start TCP fallback grace timer<br/>(4 seconds)
UdpServer->>UdpServer: Send IPv4 broadcast announcement<br/>(every 1 second for the first minute,<br/>then every 3 seconds)
UdpServer-->>UdpDisc: UDP announcement on 5560<br/>FoosVisionRecorder|proto=1|app=...
UdpDisc->>UdpDisc: Filter identity and protocol version

loop while disconnected
    Conn->>UdpDisc: GetCandidatesRankedSnapshot()

    alt UDP candidate found
        UdpDisc-->>Conn: RecorderDiscoveryCandidate(ip, appVersion, proto)
    else no new UDP candidate, viewer foreground, and fallback grace elapsed
        autonumber 200 1
        Note over Conn,Fallback: Fallback runs only while the viewer is foreground, no UDP candidate is available, and the 4-second grace timer has elapsed.
        Conn->>Fallback: GetCandidatesAsync(ct)
        Conn->>Conn: Restart TCP fallback grace timer
        Fallback->>Fallback: Build local WiFi /24 probe list
        Fallback->>HServer: TCP connect probe to ip:5555<br/>no HelloRequest
        HServer-->>Fallback: TCP port accepted
        Fallback-->>Conn: direct-probe candidates
    else viewer background
        AndroidLifecycle->>Fallback: Disable TCP fallback probing
        Fallback-->>Conn: no fallback probing while background
    else no new UDP candidate and fallback grace still running
        Conn->>Conn: Wait PollInterval (200 ms),<br/>then check for candidates again
    end

    opt candidate available from UDP discovery or TCP probing
        autonumber 300 1
        Note over Conn,HClient: Handshake starts as soon as a candidate is available, regardless of whether it came from UDP discovery or TCP probing.
        Conn->>Conn: Pick next candidate
        Conn->>Conn: LocalIpPicker.PickLocalIPv4ForRemote(recorderIp)
        Conn->>HClient: HelloAsync("tcp://ip:5555", HelloRequest)

        HClient->>HServer: NetMQ HelloRequest
        HServer->>HHandler: Handle(request)
        HHandler->>RecNet: TryAcceptViewer(request)

        alt handshake timeout or failed candidate
            HClient-->>Conn: timeout / failed handshake
            Conn->>UdpDisc: RemoveCandidate(ip)
        else recorder already has viewer
            RecNet-->>HHandler: false
            HHandler-->>HClient: HelloResponse Accepted=false, RecorderBusy
            HClient-->>Conn: busy
            Conn->>UdpDisc: RemoveCandidate(ip)
        else accepted
            RecNet-->>HHandler: true
            HHandler-->>HClient: HelloResponse Accepted=true
            HHandler->>Camera: via onHello: ConfigureUdpVideoStream(viewerIp, 5561)
            HClient-->>Conn: protocol/app/settings/diagnostics
            Conn-->>ViewNet: RecorderConnectionResult.Connected
            ViewNet->>ViewNet: create command/event/live subscribers
            ViewNet-->>ViewerRoot: connected
        end
    end
end
```

## Details

Recorder UDP announcements use this identity format:

```text
FoosVisionRecorder|proto=1|app=1.0.0
```

The protocol version is authoritative for compatibility. The app version is diagnostic metadata and is not used by the viewer to reject otherwise compatible recorders.

Discovery is IPv4-only. The recorder broadcasts on each active non-loopback local interface to `255.255.255.255`, to the interface broadcast address when available, and to a pragmatic `/24` broadcast address such as `192.168.1.255`. The announcement interval is one second for the first minute after discovery starts, then three seconds.

The Android viewer keeps one UDP discovery session open while disconnected. It checks for candidates every 200 ms when no candidate is currently available. TCP fallback probing runs only while the viewer is in the foreground, is delayed by a four-second grace period, and can run again only after another four seconds. There is no separate pairing-cycle timeout; a started TCP fallback probe run is not interrupted by an outer discovery budget. Real handshake attempts are bounded per candidate by a three-second timeout.

TCP fallback probing is only a candidate source. It scans the viewer's active WiFi `/24`, uses short TCP connect attempts against port `5555`, and does not send a `HelloRequest`. A candidate becomes a usable recorder only after the normal FoosVision handshake succeeds.

## Handshake And Video

Both discovery paths use the same handshake endpoint:

```text
tcp://<recorder-ip>:5555
```

The viewer sends its selected local IPv4 address in the `HelloRequest`. The recorder returns protocol version, recorder app version, diagnostics settings, and viewer runtime settings. The handshake client uses a fresh NetMQ request socket per attempt so timeouts or failed candidates do not poison later attempts.

After a successful handshake, the recorder streams RTP/H.264 over UDP to the viewer on port `5561`. On Android, the recorder binds the app process to WiFi while active and binds the RTP socket to the local IPv4 address that matches the viewer route. This avoids sending video through the wrong interface when WiFi and mobile data are both active.

The recorder keeps discovery active after a viewer connects. Additional viewer handshakes are rejected while one viewer is connected, but the recorder remains discoverable.

## Operational Interpretation

Useful log indicators:

- `DiscoveryAppVersion=1.0.0`: UDP announcement path worked.
- `DiscoveryAppVersion=android-direct-probe`: UDP discovery did not provide a candidate after the fallback grace period; TCP probing found the recorder.
- `Android recorder fallback probing enabled because the viewer is in the foreground.`: TCP fallback probing is allowed again after a foreground transition.
- `Android recorder fallback probing disabled because the viewer is not in the foreground.`: TCP fallback probing is blocked after a background transition.
- `Android recorder fallback probing run started`: one TCP fallback probing run started. This is logged once per run, not once per probed IP address.
- `Android recorder fallback probing run completed`: one TCP fallback probing run finished, including the number of found candidates.
- `Android recorder fallback local WiFi addresses. Addresses=<none>`: the viewer could not determine an active WiFi IPv4 address.
- `Acquired WiFi multicast lock for viewer discovery.`: Android acquired the multicast lock used while the viewer page runtime is attached.

Known practical behavior:

- Some Android/router combinations do not reliably deliver UDP broadcast announcements to the viewer.
- The TCP fallback is more active than pure beacon discovery, but it is limited to the viewer's local `/24`, uses short timeouts, and runs only while disconnected and foregrounded.
- A future cleaner replacement would be an explicit UDP query-response discovery path or mDNS/Bonjour-style discovery. The current TCP fallback is a pragmatic reliability measure for local two-device setups.
