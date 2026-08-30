# Performance Notes

This document records practical runtime findings from Android performance testing. It is intentionally observation-driven: the numbers below describe what was measured in the current implementation and should be updated when the pipeline changes.

## Pixel 7 Recorder

### Test setup

- Recorder: Google Pixel 7
- Viewer: Samsung Galaxy S11 tablet
- Network: `5 GHz` WLAN
- Runtime metrics: enabled with a `10s` report interval
- Workload: simultaneous `120 fps` H.264 live stream and `30 fps` decoded preview / tracking path

### Long-Run Behavior

Observed behavior over time:

- During the first minutes, the `120 fps` H.264 stream stayed close to target on producer, sender, receiver, decoder push, and rendered-frame metrics.
- During the same initial period, the tracking path stayed close to `30 fps`.
- After several minutes, the H.264 stream was still largely healthy, but the decoded / tracking path started drifting below `30 fps`.
- After a long run of roughly one hour, the H.264 stream still stayed around `120 fps`, while the decoded / tracking path was closer to `19-20 fps`.

Representative late-run measurements on Pixel 7:

- `Android.OnFrameAvailable.CallbackInterval`: about `19-20/s`
- `Android.OnFrameAvailable.CameraTimestampInterval`: about `19-20/s`
- `Android.Recorder.LiveFrame.AcceptedInterval`: about `19-20/s`
- `Android.Recorder.NetMq.TrackingFrameSendInterval`: about `19-20/s`
- `Android.Viewer.NetMq.TrackingFrameReceiveInterval`: about `19-20/s`
- `Android.Marshal.Copy`: about `39-42ms` average
- `Android.GlReadPixels`: about `7-8ms` average
- `Android.Recorder.Vision.DetectBallsDuration`: about `32-35ms` average
- `Android.Recorder.LiveFrame.ProcessDuration`: about `32-36ms` average

### Interpretation

The H.264 high-speed path and the decoded tracking path behave differently:

- The `120 fps` H.264 path is hardware-encoder-centric and remained stable during the long Pixel 7 run.
- The `30 fps` tracking path depends on preview delivery, GPU readback, RGBA copy, and CPU vision processing. This path degraded over time on the Pixel 7.
- Because `OnFrameAvailable`, accepted live frames, tracking-frame send intervals, viewer receive intervals, and viewer handle intervals all dropped to the same rate, the bottleneck is recorder-side before or at the decoded / tracking path, not the tracking NetMQ transport and not the viewer handling.
- `Marshal.Copy` consumes the largest measured part of the late-run frame budget. At around `40ms`, it is too slow to sustain `30 fps` by itself.
- The lower `OnFrameAvailable` rate shows that `Marshal.Copy` is probably not the only isolated cause. It is more likely part of a backpressure / thermal-throttling effect in the preview-readback pipeline.
- Vision processing also becomes budget-relevant late in the run. It is not the first indication of the problem, but after throttling it no longer leaves much margin inside a `33.333ms` frame budget.

Network observations:

- `2.4 GHz` WLAN produced visible viewer-side stalls for the H.264 stream in earlier tests.
- Switching to `5 GHz` removed the large stalls in the good runs and made the viewer feel smooth.
- Occasional viewer-side H.264 dips, when producer and sender remain stable, point to receive-side network jitter or viewer-side packet delivery rather than decoder throughput.
