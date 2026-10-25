# Protocol and Connectivity Guide

The repo already contains protocol and connectivity abstractions for recorder-viewer communication.
Keep protocol contracts separate from concrete transport implementations.

Relevant projects and folders:

- `Product/Adapters/FoosVision.Protocol`
  - protocol message definitions, protocol versioning, default ports, and connectivity abstractions
- `Product/Infrastructure/FoosVision.NetMq`
  - concrete NetMQ transport for handshake, commands, events, live data, and live analysis
- `Product/Infrastructure/FoosVision.NetDiscovery`
  - FoosVision recorder discovery adapter
- `Product/Infrastructure/NetDiscovery`
  - lower-level UDP discovery implementation
- `Experiments/Protocol/RecorderCli`
- `Experiments/Protocol/ViewerCli`

Existing protocol concepts include:

- handshake
- discovery
- commands
- replies
- events
- live data
- live analysis
- default ports
- protocol versioning

Important rule:
A major version increase is treated as a breaking protocol change.

If you change protocol messages, ports, handshake behavior, discovery behavior, transport channels, or version semantics, call that out explicitly.
