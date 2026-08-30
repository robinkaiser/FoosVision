// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Media;
using Android.OS;
using Android.Views;
using FoosVision.Common.Logging;
using FoosVision.Common.Metrics;
using FoosVision.Media.Android.Common;
using FoosVision.Media.Core.EncodedVideo;
using Java.Nio;

namespace FoosVision.Media.Android.CameraFeed;

internal class Encoder : MediaCodec.Callback
{
    private enum State
    {
        NotStarted,
        Running,
        Stopped,
    }

    private static readonly Source _Log = new("Media.Android.H264Encoder");

    private readonly Handler _Handler;
    private readonly MediaFormat _Format;

    private readonly IEncodedAccessUnitSink _EncodedSink;
    private readonly IntervalMetric? _OutputAccessUnitInterval;
    private MediaCodec? _Codec;
    private Surface? _InputSurface;
    private volatile State _State = State.NotStarted;

    private Encoder(
        Handler handler,
        string mimeType,
        MediaFormat format,
        IEncodedAccessUnitSink encodedSink,
        RuntimeMetricsOptions runtimeMetricsOptions)
    {
        _Handler = handler;
        _Format = format;
        _EncodedSink = encodedSink;

        if (runtimeMetricsOptions.Enabled)
        {
            _OutputAccessUnitInterval = new IntervalMetric(
                runtimeMetricsOptions.CreateMetricName("Recorder.H264Producer.AccessUnitInterval"),
                _Log,
                runtimeMetricsOptions.GetReportInterval());
        }

        _Codec = MediaCodec.CreateEncoderByType(mimeType);

        // Deliver callbacks on _Handler thread.
        _Codec.SetCallback(this, _Handler);
        _Codec.Configure(_Format, null, null, MediaCodecConfigFlags.Encode);

        _InputSurface = _Codec.CreateInputSurface();

        _Log.Information("Configured encoder: {0}", _Codec.Name);
    }

    public Surface InputSurface => _InputSurface ?? throw new InvalidOperationException("Encoder not initialized.");

    public static Task<Encoder> CreateAsync(
        Handler handler,
        string mimeType,
        MediaFormat format,
        IEncodedAccessUnitSink encodedSink,
        RuntimeMetricsOptions? runtimeMetricsOptions = null)
    {
        var tcs = new TaskCompletionSource<Encoder>(TaskCreationOptions.RunContinuationsAsynchronously);
        RuntimeMetricsOptions options = runtimeMetricsOptions ?? RuntimeMetricsOptions.CreateDefault();

        handler.Post(() =>
        {
            try
            {
                var enc = new Encoder(handler, mimeType, format, encodedSink, options);
                tcs.TrySetResult(enc);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    public Task StartAsync()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _Handler.Post(() =>
        {
            try
            {
                if (_State == State.Running)
                {
                    tcs.TrySetResult();
                    return;
                }

                if (_State == State.Stopped)
                {
                    tcs.TrySetException(new InvalidOperationException("Encoder instance is stopped and cannot be restarted. Create a new Encoder."));
                    return;
                }

                if (_State != State.NotStarted)
                {
                    tcs.TrySetException(new InvalidOperationException($"Start in state {_State}"));
                    return;
                }

                _Codec!.Start();
                _State = State.Running;
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    public Task StopAsync()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _Handler.Post(() =>
        {
            try
            {
                if (_State == State.Stopped)
                {
                    tcs.TrySetResult();
                    return;
                }

                // Terminal transition: after StopAsync the instance cannot be reused.
                _State = State.Stopped;

                TryIgnore(() => _Codec?.SignalEndOfInputStream(), "Codec.SignalEndOfInputStream");
                TryIgnore(() => _Codec?.Stop(), "Codec.Stop");
                TryIgnore(() => _Codec?.Release(), "Codec.Release");
                TryIgnore(() => _InputSurface?.Release(), "InputSurface.Release");

                _Codec?.Dispose();
                _Codec = null;

                _InputSurface?.Dispose();
                _InputSurface = null;

                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    public override void OnError(MediaCodec codec, MediaCodec.CodecException e)
    {
        _Log.Error("MediaCodec error: {0}", e);
    }

    public override void OnInputBufferAvailable(MediaCodec codec, int index)
    {
        // Not used with Surface input.
    }

    public override void OnOutputFormatChanged(MediaCodec codec, MediaFormat format)
    {
        _Log.Information("Encoder output format changed: {0}", format);

        try
        {   // Push CSD (VPS/SPS/PPS) into NAL sink in Annex-B form.
            // CSD keys are csd-0, csd-1, ...; count/packing varies by codec/device (HEVC often only csd-0).
            bool sawAny = false;

            for (int i = 0; i < 8; i++)
            {
                bool wrote = WriteCsdIfPresent(format, $"csd-{i}");

                if (wrote)
                {
                    sawAny = true;
                }
                else if (sawAny)
                {   // stop after first gap once we've seen at least one
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _Log.Warning("OnOutputFormatChanged: Failed to write CSD: {0}", ex);
        }
    }

    public override void OnOutputBufferAvailable(MediaCodec codec, int index, MediaCodec.BufferInfo info)
    {
        try
        {
            if (_State != State.Running)
            {
                codec.ReleaseOutputBuffer(index, false);
                return;
            }

            if (info.Size <= 0)
            {
                codec.ReleaseOutputBuffer(index, false);
                return;
            }

            var bb = codec.GetOutputBuffer(index);

            if (bb == null)
            {
                codec.ReleaseOutputBuffer(index, false);
                return;
            }

            int written = AnnexBConverter.WriteAccessUnit(bb, info, _EncodedSink.Buffer, _EncodedSink.Offset);

            if (written > 0)
            {
                long tsNs = info.PresentationTimeUs * 1000L;
                _OutputAccessUnitInterval?.Record(tsNs, 1000000000);
                _EncodedSink.Completed(tsNs, written);
            }

            codec.ReleaseOutputBuffer(index, false);
        }
        catch (Exception ex)
        {
            _Log.Warning("OnOutputBufferAvailable failed: {0}", ex);
            TryIgnore(() => codec.ReleaseOutputBuffer(index, false), "Codec.ReleaseOutputBuffer");
        }
    }

    private bool WriteCsdIfPresent(MediaFormat format, string key)
    {
        ByteBuffer? csd = format.GetByteBuffer(key);
        if (csd == null) return false;

        var dst = _EncodedSink.Buffer;
        int dstOffset = _EncodedSink.Offset;

        int written = AnnexBConverter.WriteCsd(csd, dst, dstOffset);

        if (written > 0)
        {
            _EncodedSink.Completed(0, written);
            return true;
        }

        return false;
    }

    private static void TryIgnore(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _Log.Error("Release cleanup for {What} failed with exception {Ex}.", what, ex);
        }
    }
}
