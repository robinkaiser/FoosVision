# Configuration

FoosVision currently supports a user-editable configuration file for the `Recorder`. The `Viewer` does not have its own `Config.json`. It receives all relevant settings from the recorder during the handshake.

Changes to `Config.json` are read only at recorder startup. Restart the recorder after editing the configuration. Connected viewers keep the settings received during the handshake. Reconnect or restart viewers after the recorder restart if viewer behavior should use changed recorder settings.

## Recorder Config

The recorder creates or updates `Config.example.json` at startup. If `Config.json` is missing, it is created from `Config.example.json`. Existing `Config.json` files are never overwritten automatically during startup. If `Config.json` is invalid, the app starts with defaults, logs the config error, and leaves the invalid file unchanged.

Typical location:

```text
/storage/emulated/0/Android/data/org.foosvision.app/files/FoosVision.Recorder/Config.json
```

If the app falls back to internal app data, use the `SettingsPath` value from the recorder startup log or Seq event to locate the actual directory.

## Editing Config.json

The recorder app includes a basic `Config.json` text editor. Open `Config` on the recorder screen,
edit the JSON, and save it. The recorder validates the edited JSON before writing it. If validation
fails, the existing `Config.json` stays unchanged and the editor shows the validation error.
If the app started with defaults because `Config.json` is invalid, the editor still opens the invalid
file so it can be fixed. Saving valid JSON overwrites that invalid file with the corrected content.
Restart the recorder after saving changes. Restart or reconnect connected viewers after the recorder
restart so they receive the changed recorder settings during the next handshake.

Alternative PC workflow:

1. Connect the recorder phone by USB.
2. Select the USB file transfer option on the phone.
3. Copy `Config.json` from the recorder app files directory to the PC.
4. Edit the copied file with a plain-text editor.
5. Copy the updated file back to the same recorder app files directory.
6. Restart the recorder app.

## Example Config

`Config.example.json` is generated from the recorder defaults and currently has this structure:

```json
{
  "version": 1,
  "viewer": {
    "liveVideo": {
      "playbackBufferMilliseconds": 25,
      "maxPlaybackBufferMilliseconds": 100,
      "decoderLowLatency": true,
      "udpReceiveBufferBytes": 2097152
    }
  },
  "diagnostics": {
    "logging": {
      "file": {
        "enabled": true,
        "minimumLevel": "Information",
        "format": "CompactJson",
        "rollingInterval": "Day",
        "retentionDays": 7,
        "retainedFileCountLimit": 7
      },
      "seq": {
        "enabled": false,
        "serverUrl": "http://192.168.178.50:5341",
        "apiKey": "",
        "minimumLevel": "Debug",
        "sendTestEventOnStartup": true
      }
    },
    "video": {
      "enabled": false,
      "retentionDays": 1,
      "maxTotalSizeBytes": 1073741824
    },
    "runtimeMetrics": {
      "enabled": false,
      "reportIntervalSeconds": 10
    },
    "vision": {
      "debugVisualizations": {
        "showObservations": false,
        "showBallDetectionMask": false
      }
    }
  }
}
```

## Viewer Settings

The recorder sends `viewer` settings to connected viewers during the handshake. The `Viewer` does not read a local `Config.json`.

- `viewer.liveVideo.playbackBufferMilliseconds`: live playback buffer duration before playback starts. Lower values reduce baseline latency and leave less margin for jitter.
- `viewer.liveVideo.maxPlaybackBufferMilliseconds`: maximum live access-unit backlog duration before the viewer drops queued access units and waits for the next keyframe. This must be greater than `playbackBufferMilliseconds`.
- `viewer.liveVideo.decoderLowLatency`: enables Android MediaCodec low-latency mode for the live decoder.
- `viewer.liveVideo.udpReceiveBufferBytes`: UDP socket receive buffer requested by the viewer for RTP packets.

Default values target low live latency:

```json
{
  "viewer": {
    "liveVideo": {
      "playbackBufferMilliseconds": 25,
      "maxPlaybackBufferMilliseconds": 100,
      "decoderLowLatency": true,
      "udpReceiveBufferBytes": 2097152
    }
  }
}
```

For less aggressive playback on weaker hardware or less stable networks, a first conservative test value is:

```json
{
  "viewer": {
    "liveVideo": {
      "playbackBufferMilliseconds": 50,
      "maxPlaybackBufferMilliseconds": 200,
      "decoderLowLatency": true,
      "udpReceiveBufferBytes": 2097152
    }
  }
}
```

## Diagnostics Settings

The `diagnostics` section controls diagnostics behavior:

- `diagnostics.logging.file`: local compact JSON file logs.
- `diagnostics.logging.seq`: optional Seq sink and the Seq settings shared with connected viewers.
- `diagnostics.video`: recorder-side diagnostic video artifacts for both install and game sessions.
- `diagnostics.runtimeMetrics`: periodic runtime metric logging.
- `diagnostics.vision.debugVisualizations`: vision debug overlays.

To send logs to Seq, enable the recorder Seq sink and set `serverUrl` to the HTTP URL reachable from the Android devices.
The viewer does not read its own Seq config; it receives the recorder's Seq settings during the handshake.

```json
{
  "diagnostics": {
    "logging": {
      "seq": {
        "enabled": true,
        "serverUrl": "http://192.168.1.6:5341",
        "minimumLevel": "Debug",
        "sendTestEventOnStartup": true
      }
    }
  }
}
```

Supported diagnostics logging values:

- `minimumLevel`: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`.
- `file.format`: `CompactJson`, `Text`.
- `file.rollingInterval`: `Infinite`, `Year`, `Month`, `Day`, `Hour`, `Minute`.

Validation rules:

- Unknown JSON properties are rejected.
- `logging.seq.serverUrl` must be an absolute `http` or `https` URL.
- Retention values must be zero or greater. A value of zero disables that retention limit.
- `diagnostics.video.maxTotalSizeBytes` and `diagnostics.runtimeMetrics.reportIntervalSeconds` must be at least `1`.
- `viewer.liveVideo.playbackBufferMilliseconds` must be zero or greater.
- `viewer.liveVideo.maxPlaybackBufferMilliseconds` must be greater than `playbackBufferMilliseconds` and less than or equal to `1000`.
- `viewer.liveVideo.udpReceiveBufferBytes` must be at least `524288`.

Operational diagnostics workflows are documented in [Diagnostics](Diagnostics.md).
