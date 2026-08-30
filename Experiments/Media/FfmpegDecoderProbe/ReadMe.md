## FFmpeg version selection notes

### Problem

The Windows `VideoPlayer` and this minimal probe both showed the same behavior:

- without debugger: `ffmpeg.avcodec_open2(...)` succeeded
- with Visual Studio debugger attached: the process exited while entering `avcodec_open2(...)`
- exit code: `0x406d1388`

This made normal `F5` startup debugging impossible, because the process terminated before decoder setup completed.

### Isolation

To rule out recorder-specific code, `FfmpegDecoderProbe` was created as a minimal repro:

- initialize FFmpeg runtime
- call `avcodec_find_decoder(...)`
- call `avcodec_alloc_context3(...)`
- call `avcodec_open2(...)`

Result:

- the issue reproduced in the probe as well
- therefore the bug was not in `RecorderHost`, `VideoPlayer`, networking, handshake, MP4 handling, or use-case code
- the failure was isolated to the native FFmpeg decoder open path under debugger

### Compared builds

#### Old build (problematic)

Version:

- `ffmpeg version 8.0.1-full_build-www.gyan.dev`
- `libavcodec 62.11.100`

Observed behavior:

- works without debugger
- exits with `0x406d1388` under debugger during `avcodec_open2(...)`

#### New build (works)

Version:

- `ffmpeg version N-123511-g3e8bec7871-20260316`
- `libavcodec 62.29.100`

Source:

- <https://github.com/BtbN/FFmpeg-Builds/releases>

Observed behavior:

- works without debugger
- works with Visual Studio debugger attached
- allows normal `F5` startup and breakpoints in the recorder/video player flow

### Important finding

The original working hypothesis was that the difference might be `w32threads`.
That turned out to be wrong.

Both compared builds reported:

- `--disable-w32threads`

So the fix did not come from switching to `w32threads`.

### Current assumption

The most likely explanation is the combination of:

- newer FFmpeg revision
- different Windows build/distribution
- different toolchain/runtime details
- different pthreads/MinGW environment

This is an informed assumption, not a proven root cause at source-code level.
What is proven is:

- the debugger crash was FFmpeg-build-specific
- switching from the old `gyan.dev` build to the newer `BtbN` build resolved the issue for this repository

### Practical conclusion

For FoosVision development on Windows, use the FFmpeg build from:

- <https://github.com/BtbN/FFmpeg-Builds/releases>

Do not use the previously tested build from:

- `8.0.1-full_build-www.gyan.dev`

for debugger-driven startup scenarios, because it reproduced the `avcodec_open2(...)` debugger crash in this repo.
