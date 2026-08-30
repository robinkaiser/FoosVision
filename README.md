# FoosVision

FoosVision is an Android-based foosball vision system. It uses a `Recorder` phone mounted above the table to capture and analyze the game, and a `Viewer` device to show the live stream, tracked-ball visualization, and slow-motion replay for shots.

The current product focus is low-latency live viewing, 30 fps live ball tracking, shot detection, and 120 fps replay analysis with shot metrics. For setup examples and demos, see the [FoosVision YouTube channel](https://www.youtube.com/@FoosVision).

## Apps

FoosVision is distributed through Google Play and provides two launcher icons:

- `Recorder`: use this on the Android smartphone mounted above the table.
- `Viewer`: use this on an Android smartphone or tablet connected to the recorder.

Install FoosVision on both devices. Start `Recorder` on the recorder phone and `Viewer` on the viewing device.

## Requirements

- Recorder and viewer devices must run Android 13 or newer.
- Recorder device must support FullHD video at `120 fps`.
- Use a stable `5 GHz` Wi-Fi network for reliable `120 fps` playback. Treat `2.4 GHz` Wi-Fi as best-effort only.
- Disable battery optimization and power-saving restrictions for FoosVision when possible. Non-stock Android devices (e.g. Samsung, Xiaomi) may apply aggressive power management that can affect streaming or tracking stability.
- Mount the recorder centered or slightly offset above the table, so the full playing field is visible and fills most of the camera image.
- Keep the recorder fixed and vibration-free; a boom stand is a suitable mounting option. Attaching the recorder directly to the table is not recommended because table jarring can make it shake.
- Use a white ball; other colors are currently not supported.
- Use colored or black players; white or gray players are currently not supported.

## Basic Usage

1. Start `Recorder` on the phone mounted above the table.
2. Start `Viewer` on the viewing device; it searches for recorders on the local network.
3. Start installation and mount the recorder above the table. Installation can only be started after the viewer has connected to the recorder.
4. Wait until the table is detected. The playing field and optionally a horizontally mounted light should be outlined, and the rods should be marked in the player colors.
5. Stop installation and start a game session. A game can only be started after a successful installation.

## Performance Notes

The recorder performs continuous high-speed camera capture, video encoding, streaming, and vision processing. This can put significant load on the phone, and the device may become warm during longer sessions.

The viewer shows two frame rates. During live game operation, `Stream` should stay close to `120 fps`, and `Tracking` should stay close to `30 fps`. During replay, `Tracking` should show `120 fps`. If these values drop, the recorder device, network, or playback device may be overloaded.

## Documentation

- [Configuration](Documentation/Configuration.md): App configuration handling and example config.
- [Diagnostics](Documentation/Diagnostics.md): Diagnostics settings and Seq setup for debugging.

## Development

FoosVision is built with `.NET`, `.NET MAUI`, and C#.

Install Visual Studio with the `.NET MAUI` workload and a .NET SDK version that supports the repository target framework. Then open `FoosVision.slnx` and build the solution.

For development and testing, `VideoPlayer` can be used as a recorder replacement to stream prerecorded videos to the viewer.

## Legal

FoosVision is licensed under the [GNU General Public License v3.0](LICENSE.md). The software is provided without warranty; see the license for details.

Participation in this project is covered by the [Code of Conduct](CODE-OF-CONDUCT.md).

See the [Privacy Policy](PRIVACY.md) for app data processing details.

Third-party component notices are listed in [Third-Party Notices](THIRD-PARTY-NOTICES.md).
