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

## Recorder Announcements

At recorder startup, the recorder starts a UDP announcement server and publishes this identity:

```text
FoosVisionRecorder|proto=1|app=1.0.0
```

The protocol version is authoritative for compatibility. The app version is diagnostic metadata and is not used by the viewer to reject otherwise compatible recorders.

The recorder sends announcements periodically to IPv4 broadcast addresses for each active non-loopback local interface:

- `255.255.255.255`
- the interface broadcast address, when the platform exposes a mask
- a pragmatic `/24` broadcast address, for example `192.168.1.255` for `192.168.1.x`

Loopback interfaces and loopback addresses are ignored. IPv6 announcements are skipped; the current recorder-viewer discovery path is IPv4-only.

The recorder keeps discovery active after a viewer connects. Additional viewer handshakes are rejected while one viewer is connected, but the recorder remains discoverable.

Expected recorder logs:

```text
Recorder discovery started. Port=5560 Identity=FoosVisionRecorder|proto=1|app=1.0.0
Recorder handshake endpoint started. BindAddress=tcp://*:5555
Recorder discovery announcement sent. LocalAddress=192.168.1.3 TargetAddress=255.255.255.255 Port=5560 Identity=FoosVisionRecorder|proto=1|app=1.0.0
```

## Viewer UDP Listener

The viewer starts recorder discovery when the viewer session initializes and keeps the UDP listener open until connection succeeds or the viewer session is disposed.

Expected viewer logs:

```text
Connecting viewer to recorder.
Starting recorder discovery. ExpectedIdentity=FoosVisionRecorder|proto=1|app=*
```

On Android, the viewer also acquires a WiFi multicast lock while the viewer page runtime is attached:

```text
Acquired WiFi multicast lock for viewer discovery.
```

When a compatible UDP announcement arrives, the viewer turns it into a recorder candidate and attempts the handshake:

```text
Trying discovered recorder. RecorderIp=192.168.0.179 DiscoveryAppVersion=1.0.0 ProtocolVersion=1
```

`DiscoveryAppVersion=1.0.0` indicates that the candidate came from the recorder UDP announcement identity.

## Android TCP Fallback

If the viewer does not receive a usable UDP announcement, it uses an Android-specific fallback candidate source.

The Android fallback reads the active WiFi IPv4 address through Android platform APIs. For example, if the viewer address is `192.168.1.4`, it probes these candidate addresses:

```text
192.168.1.1:5555
192.168.1.2:5555
...
192.168.1.254:5555
```

The fallback uses short TCP connect probes with bounded parallelism. A successful TCP connect only means that something is listening on the handshake port. The viewer still performs the normal FoosVision handshake before accepting the recorder.

Expected viewer logs:

```text
Android recorder fallback local WiFi addresses. Addresses=192.168.1.4
Android recorder fallback probing local subnet for recorder handshake endpoint. AddressCount=253 Port=5555
Android recorder fallback found handshake endpoints. Count=1 Addresses=192.168.1.2
Trying discovered recorder. RecorderIp=192.168.1.2 DiscoveryAppVersion=android-direct-probe ProtocolVersion=1
```

`DiscoveryAppVersion=android-direct-probe` indicates that the candidate came from the TCP fallback, not from UDP discovery.

## Retry Model

The viewer has one long-lived discovery session. It no longer closes and recreates discovery every few seconds.

Instead, it repeats bounded pairing cycles while the discovery session stays open:

1. Read UDP discovery candidates.
2. If no new UDP candidate exists, run the fallback candidate source.
3. Try each untried candidate within the current pairing budget.
4. If no connection succeeds, start another pairing cycle.

Typical failed-cycle log:

```text
Recorder discovery cycle ended without connection. Failure=NoCandidateFound
```

This log does not mean the viewer stopped looking. It means only that the current pairing cycle ended without a usable candidate.

## Handshake

Both discovery paths use the same handshake endpoint:

```text
tcp://<recorder-ip>:5555
```

The viewer sends its selected local IPv4 address in the handshake request. The recorder returns protocol version, recorder app version, diagnostics settings, and viewer runtime settings.

Expected successful logs:

```text
Recorder received handshake request. ViewerAddress=192.168.1.4 ProtocolVersion=1
Recorder sending handshake response. ViewerAddress=192.168.1.4 ProtocolVersion=1 RecorderAppVersion=1.0.0 Accepted=true
Viewer connected to recorder. RecorderIp=192.168.1.2 ProtocolVersion=1 RecorderAppVersion=1.0.0
Viewer handshake diagnostics applied. RecorderIp=192.168.1.2 ...
```

The handshake client uses a fresh NetMQ request socket per attempt so timeouts or failed candidates do not poison later attempts.

## Live Video Binding

Live video is not part of discovery, but it depends on the connection result.

After a successful handshake, the recorder streams RTP/H.264 over UDP to the viewer. On Android, the recorder binds the app process to the available WiFi network while the recorder runtime is active. The recorder also binds the RTP socket to the local IPv4 address that matches the viewer's IP route. This avoids sending RTP through the wrong local interface when the recorder device has multiple active network paths, for example WiFi plus mobile data.

This addresses the observed failure mode where commands arrived over TCP but no live image appeared until mobile data was disabled.

## Operational Interpretation

Useful indicators:

- `DiscoveryAppVersion=1.0.0`: UDP announcement path worked.
- `DiscoveryAppVersion=android-direct-probe`: UDP discovery did not provide the candidate; TCP fallback found the recorder.
- `Recorder discovery cycle ended without connection`: viewer is still running but did not find a candidate in that cycle.
- `Android recorder fallback local WiFi addresses. Addresses=<none>`: viewer could not determine an active WiFi IPv4 address.

Known practical behavior:

- Some Android/router combinations do not reliably deliver UDP broadcast announcements to the viewer.
- The TCP fallback is more active than pure beacon discovery, but it is limited to the viewer's local `/24`, uses short timeouts, and runs only while disconnected.
- A future cleaner replacement would be an explicit UDP query-response discovery path or mDNS/Bonjour-style discovery. The current TCP fallback is a pragmatic reliability measure for local two-device setups.
