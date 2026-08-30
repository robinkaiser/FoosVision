# Streaming Notes

## VideoPlayer -> Viewer RTP/H264

The current `VideoPlayer -> Viewer` path uses `RTP/H264 over UDP`. The `Viewer` treats `VideoPlayer` and `Recorder` as interchangeable counterparts.

Current practical findings from Android viewer testing with Pixel 7:

- `120 fps` playback on Android is generally viable.
- `1` keyframe and `4` keyframes per second can both work reliably.
- `B`-frames are not required and should stay disabled for low-latency test material.
- The earlier `raw Annex-B over UDP` path did not provide reliable playback in the Android viewer during testing.
- Within the `RTP/H264 over UDP` path, encoded bitrate was the dominant stability factor observed so far.

Observed behavior with `1920x1080`, `120 fps`, `H.264 High`:

- Around `8-10 Mbit/s`: stable playback in the Android viewer during local WLAN testing.
- Around `15-16 Mbit/s`: visible artifacts and unstable playback, even when RTP transport was already in place.

Implication:

- When preparing `VideoPlayer` test files, keep bitrate under control.
- Do not assume that increasing keyframe frequency alone is the problem.
- A re-encode can change multiple stream properties at once; compare the full stream metadata, not only GOP length.

## ffprobe Commands

Use these commands to inspect source and test files:

```powershell
ffprobe -v error -select_streams v:0 -show_entries stream=codec_name,profile,level,width,height,pix_fmt,r_frame_rate,avg_frame_rate,bit_rate,max_bit_rate,refs,has_b_frames,extradata_size -show_entries format=duration,bit_rate -of default=noprint_wrappers=1 "D:\path\to\file.mp4"
```

```powershell
ffprobe -v error -select_streams v:0 -show_frames -show_entries frame=key_frame,pict_type,pkt_size,best_effort_timestamp_time -of csv "D:\Pfad\zur\datei.mp4"
```

## ffmpeg Reference Commands

Reference re-encode for low-latency `VideoPlayer` test material with `4` keyframes per second at `120 fps` at `10Mbit/s`:

```powershell
ffmpeg -i input.mp4 -c:v libx264 -preset medium -crf 18 -bf 0 -g 30 -keyint_min 30 -sc_threshold 0 -pix_fmt yuv420p -maxrate 10M -bufsize 10M output_120fps_4kf_10mbit.mp4
```

Meaning of the key options:

- `-bf 0`: disable `B`-frames
- `-g 30`: keyframe every `30` frames, which equals `4` keyframes per second at `120 fps`
- `-keyint_min 30`: keep GOP length stable
- `-sc_threshold 0`: avoid extra scene-cut keyframes
- `-maxrate` and `-bufsize`: keep bitrate peaks in a range the Android viewer tolerated well in current tests
