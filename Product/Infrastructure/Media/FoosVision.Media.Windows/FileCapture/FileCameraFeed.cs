// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Media.Core.Capture;
using FoosVision.Media.Core.DecodedFrames;
using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Windows.Decoding;
using FoosVision.Media.Windows.FileCapture.Mp4;

namespace FoosVision.Media.Windows.FileCapture;

public class FileCameraFeed : ICameraFeed, IDisposable
{
    private static readonly Source _Log = new("Media.Windows.FileCameraFeed");

    private readonly Lock _Lock = new();
    private readonly IMp4AccessUnitSourceFactory _AccessUnitSourceFactory;
    private readonly IWindowsVideoDecoderFactory _VideoDecoderFactory;

    private IMp4AccessUnitSource? _AccessUnitSource;
    private IWindowsVideoDecoder? _VideoDecoder;
    private CancellationTokenSource? _PlaybackCancellation;
    private Task? _PlaybackTask;
    private bool _Configured;
    private bool _Disposed;

    public FileCameraFeed(FileCameraFeedOptions options)
        : this(options, new FfmpegMp4AccessUnitSourceFactory(), new WindowsVideoDecoderFactory())
    {
    }

    internal FileCameraFeed(
        FileCameraFeedOptions options,
        IMp4AccessUnitSourceFactory accessUnitSourceFactory,
        IWindowsVideoDecoderFactory videoDecoderFactory)
    {
        Options = options;
        _AccessUnitSourceFactory = accessUnitSourceFactory;
        _VideoDecoderFactory = videoDecoderFactory;
    }

    public event Action? PlaybackCompleted;

    public FileCameraFeedOptions Options { get; }

    public bool IsHardwareDecodingActive => _VideoDecoder?.IsHardwareAccelerated ?? false;

    public Task<bool> Configure()
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        lock (_Lock)
        {
            if (_PlaybackTask != null)
            {
                _Log.Warning("Configure called while playback task is active for '{0}'.", Options.FilePath);
                return Task.FromResult(false);
            }

            try
            {
                Options.Validate();
                DisposePipeline();

                IMp4AccessUnitSource source = _AccessUnitSourceFactory.Create();
                source.Configure(Options.FilePath);
                ValidateStreamInfo(source.StreamInfo);

                _AccessUnitSource = source;
                _Configured = true;
                _Log.Information(
                    "Configured file camera feed for '{0}' ({1}, {2}x{3}, encoded {4} fps, decoded {5} fps).",
                    Options.FilePath,
                    Options.Codec,
                    Options.Width,
                    Options.Height,
                    Options.EncodedFps,
                    Options.DecodedFps);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _Log.Error("File camera feed configure failed for '{0}'.", ex, Options.FilePath);
                DisposePipeline();
                return Task.FromResult(false);
            }
        }
    }

    public Task<bool> Start(IFrameSink frameSink, IEncodedAccessUnitSink encodedUnitSink)
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);
        ArgumentNullException.ThrowIfNull(frameSink);
        ArgumentNullException.ThrowIfNull(encodedUnitSink);

        lock (_Lock)
        {
            if (!_Configured || _AccessUnitSource == null)
            {
                _Log.Warning("Start called before configure for '{0}'.", Options.FilePath);
                return Task.FromResult(false);
            }

            if (_PlaybackTask != null)
            {
                _Log.Warning("Start called while playback is already running for '{0}'.", Options.FilePath);
                return Task.FromResult(false);
            }

            try
            {
                _AccessUnitSource.Reset();

                WindowsVideoDecoderOptions decoderOptions = new(
                    Options.Codec,
                    Options.Width,
                    Options.Height,
                    Options.OutputFormat,
                    Options.HardwareMode);

                IWindowsVideoDecoder videoDecoder = _VideoDecoderFactory.Create();
                videoDecoder.Configure(decoderOptions);
                _VideoDecoder = videoDecoder;

                _PlaybackCancellation = new CancellationTokenSource();
                PlaybackClock playbackClock = new(Options.EncodedFps, Options.DecodedFps);
                _PlaybackTask = RunPlaybackLoopAsync(frameSink, encodedUnitSink, videoDecoder, _AccessUnitSource, playbackClock, _PlaybackCancellation.Token);
                _Log.Information("Started file playback for '{0}'. Hardware decoding active: {1}.", Options.FilePath, IsHardwareDecodingActive);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _Log.Error("File camera feed start failed for '{0}'.", ex, Options.FilePath);
                _PlaybackTask = null;
                _PlaybackCancellation?.Dispose();
                _PlaybackCancellation = null;
                _VideoDecoder?.Dispose();
                _VideoDecoder = null;
                return Task.FromResult(false);
            }
        }
    }

    public async Task Stop()
    {
        if (_Disposed)
        {
            return;
        }

        Task? playbackTask;
        CancellationTokenSource? cancellation;

        lock (_Lock)
        {
            playbackTask = _PlaybackTask;
            cancellation = _PlaybackCancellation;
            _PlaybackTask = null;
            _PlaybackCancellation = null;
        }

        cancellation?.Cancel();

        if (playbackTask != null)
        {
            try
            {
                await playbackTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        cancellation?.Dispose();

        lock (_Lock)
        {
            _VideoDecoder?.Dispose();
            _VideoDecoder = null;
        }

        _Log.Information("Stopped file playback for '{0}'.", Options.FilePath);
    }

    public void Dispose()
    {
        if (_Disposed)
        {
            return;
        }

        Stop().GetAwaiter().GetResult();
        DisposePipeline();
        _Disposed = true;
        GC.SuppressFinalize(this);
    }

    private async Task RunPlaybackLoopAsync(
        IFrameSink frameSink,
        IEncodedAccessUnitSink encodedUnitSink,
        IWindowsVideoDecoder videoDecoder,
        IMp4AccessUnitSource accessUnitSource,
        PlaybackClock playbackClock,
        CancellationToken cancellationToken)
    {
        try
        {
            playbackClock.Start();

            long accessUnitIndex = 0;
            while (accessUnitSource.TryReadNextAccessUnit(out Mp4AccessUnit? accessUnit))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await playbackClock.DelayUntilAsync(accessUnitIndex, cancellationToken).ConfigureAwait(false);

                bool shouldEmitDecodedFrame = playbackClock.ShouldEmitDecodedFrame(accessUnitIndex);

                WriteEncodedAccessUnit(encodedUnitSink, accessUnit);
                videoDecoder.PushAccessUnit(
                    accessUnit.Buffer.Span,
                    accessUnit.TimestampNs,
                    accessUnit.IsKeyFrame,
                    queueDecodedFrames: shouldEmitDecodedFrame);

                if (shouldEmitDecodedFrame)
                {
                    while (videoDecoder.TryDequeueFrame(out WindowsDecodedFrame? frame))
                    {
                        using (frame)
                        {
                            WriteDecodedFrame(frameSink, frame);
                        }
                    }
                }

                accessUnitIndex++;
            }

            videoDecoder.Flush();
            while (videoDecoder.TryDequeueFrame(out WindowsDecodedFrame? frame))
            {
                using (frame)
                {
                    WriteDecodedFrame(frameSink, frame);
                }
            }

            _Log.Information("Playback completed for '{0}'.", Options.FilePath);
            PlaybackCompleted?.Invoke();
        }
        catch (OperationCanceledException)
        {
            _Log.Information("Playback loop cancelled for '{0}'.", Options.FilePath);
        }
        catch (Exception ex)
        {
            _Log.Error("Playback loop failed for '{0}'.", ex, Options.FilePath);
            throw;
        }
    }

    private static void WriteEncodedAccessUnit(IEncodedAccessUnitSink encodedUnitSink, Mp4AccessUnit accessUnit)
    {
        byte[] sourceBuffer = accessUnit.Buffer.ToArray();
        byte[] destinationBuffer = encodedUnitSink.Buffer;
        int destinationOffset = encodedUnitSink.Offset;

        if (sourceBuffer.Length > destinationBuffer.Length - destinationOffset)
        {
            throw new InvalidOperationException("Encoded access unit does not fit into the provided sink buffer.");
        }

        Buffer.BlockCopy(sourceBuffer, 0, destinationBuffer, destinationOffset, sourceBuffer.Length);
        encodedUnitSink.Completed(accessUnit.TimestampNs, sourceBuffer.Length);
    }

    private static void WriteDecodedFrame(IFrameSink frameSink, WindowsDecodedFrame frame)
    {
        IProducerFrameHandle handle = frameSink.AcquireForWrite();
        byte[] targetBuffer = handle.BufferRGBA8888;

        if (targetBuffer.Length == 0)
        {
            return;
        }

        ReadOnlySpan<byte> source = frame.AsSpan();
        if (source.Length > targetBuffer.Length)
        {
            throw new InvalidOperationException("Decoded frame does not fit into the provided frame sink buffer.");
        }

        source.CopyTo(targetBuffer);
        handle.MarkWritten(frame.TimeNs);
    }

    private void ValidateStreamInfo(Mp4VideoStreamInfo streamInfo)
    {
        if (streamInfo.Codec != Options.Codec)
        {
            throw new InvalidOperationException($"Configured codec '{Options.Codec}' does not match file codec '{streamInfo.Codec}'.");
        }

        if (streamInfo.Width != Options.Width || streamInfo.Height != Options.Height)
        {
            throw new InvalidOperationException(
                $"Configured video size '{Options.Width}x{Options.Height}' does not match file size '{streamInfo.Width}x{streamInfo.Height}'.");
        }
    }

    private void DisposePipeline()
    {
        _VideoDecoder?.Dispose();
        _VideoDecoder = null;

        _AccessUnitSource?.Dispose();
        _AccessUnitSource = null;
        _Configured = false;
    }
}
