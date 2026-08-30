# MediaCodec RGBA Decode Probe

The project links `..\..\..\..\FoosVision.Integration\FileCapture\H.264.mp4` as
`Assets/H.264.mp4` when that local validation file exists. If the file is missing,
the project still builds, but the deployed experiment reports the missing asset at runtime.

The app decodes all video samples through `FoosVision.Media.Android.Decoding.AndroidVideoDecoder`,
converts decoded frames to `FrameByteFormat.RGBA8888`, and prints the measured RGBA frame throughput
to the screen and Android debug output.
