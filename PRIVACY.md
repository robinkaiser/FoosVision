# Privacy Policy for FoosVision

Last updated: August 30, 2026

This Privacy Policy applies to the FoosVision Android app (`org.foosvision.app`).

FoosVision is an open-source app for foosball video analysis. The app can use one Android device as a Recorder and another Android device as a Viewer. The Recorder uses the camera to capture the table, provide a live video stream, detect the ball, and analyze replays. The Viewer connects to the Recorder on the local network and displays the live video, tracked-ball overlay, and replays.

## Controller

Robin Kaiser

Email: robin@foos.vision

Source code: https://github.com/robinkaiser/foosvision

## Data Processed by FoosVision

FoosVision processes data locally on the involved Android devices:

- camera images from the Recorder device for live video, ball tracking, shot detection, and replay analysis
- local network data used to connect the Recorder and Viewer on the same network
- local Recorder configuration, such as live video and diagnostics settings
- local diagnostic logs, such as technical events, errors, performance values, and connection information
- optional diagnostic video files on the Recorder, if this feature is explicitly enabled in the configuration

FoosVision does not provide user accounts. The app does not contain ads and does not use analytics or advertising SDKs.

## Purpose of Processing

The data is used to provide the app's features:

- transmit live video from the Recorder to the Viewer
- detect and display ball positions
- detect shots and analyze replays
- connect the Recorder and Viewer on the local network
- diagnose technical errors and performance issues

## Camera and Video Data

The camera is used only in the Recorder role. Camera images are processed for live video, ball tracking, and replay analysis.

The live video stream is transmitted to connected Viewers on the local network. FoosVision does not provide any feature for uploading camera images or videos to Robin Kaiser, Google, or any other external server.

Diagnostic video files are created only if the corresponding diagnostics feature is enabled in the Recorder configuration. These files are stored locally on the Recorder device and are intended for technical troubleshooting.

## Local Network

FoosVision uses network functionality so Viewer devices can find and connect to a Recorder on the local network. Technical network data such as local IP addresses, ports, connection status, and protocol events may be processed and stored in local diagnostic logs.

The Recorder announces itself on the local network and may use the device's WiFi network information to keep local video streaming on the WiFi network. The Viewer listens for local Recorder announcements and may use the device's WiFi connection information to determine the local WiFi IPv4 address. If local announcements are not received, the Viewer may probe local network addresses in the same IPv4 subnet for the FoosVision handshake port. This local probing is used only to find a Recorder on the same local network.

FoosVision does not operate a central cloud service for the app's functionality.

## Diagnostic Logs and Optional External Diagnostics

FoosVision may store local diagnostic logs on the device. These logs are used for troubleshooting and may contain technical information about app execution, configuration, connections, and performance.

FoosVision optionally supports sending diagnostic events to a user-configured Seq server. This feature is disabled by default. If enabled, diagnostic events are sent to the configured server address. The person configuring the server is responsible for its access control and security.

## Storage and Deletion

Configuration files, diagnostic logs, and optional diagnostic video files are stored locally in the Android app storage. They can be removed by clearing the app data or uninstalling the app.

FoosVision does not store user accounts on a FoosVision server. Therefore, there is no server-side account data to delete.

## Data Sharing

FoosVision does not sell personal data and does not share data for advertising purposes.

Data is transmitted outside a device only in the following situations:

- live video and technical protocol messages between the Recorder and Viewer on the local network
- diagnostic events to a user-configured Seq server, if that option is enabled
- data that the user manually exports or shares for troubleshooting

## Permissions

FoosVision uses the following Android permissions:

- `CAMERA`: camera access for the Recorder role
- `INTERNET`: local network communication between Recorder and Viewer and optional communication with a configured diagnostics server
- `ACCESS_NETWORK_STATE`: network status detection for connection features
- `ACCESS_WIFI_STATE`: WiFi connection information, such as the local WiFi IP address, for Recorder discovery and local video streaming on the local network
- `CHANGE_WIFI_MULTICAST_STATE`: allows the Viewer to receive local WiFi multicast/broadcast discovery traffic more reliably

## Children

FoosVision is not directed at children. The app is intended for users who want to record, view, or analyze foosball games.

## Open Source and Third-Party Components

FoosVision is published as open-source software under the GNU General Public License v3.0 or later. Information about third-party components and their licenses is provided in the app's third-party notices and in the source repository.

## Changes

This Privacy Policy may be updated if the app's features, data processing, or legal requirements change. The current version will be published in the public FoosVision repository.
