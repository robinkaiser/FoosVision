// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Net;
using System.Net.Sockets;
using Android.Graphics;
using Android.Views;
using FoosVision.Adapters.Viewer.Session.Playback;
using FoosVision.Common.Logging;
using FoosVision.Common.Metrics;
using FoosVision.Media.Android.Decoding;
using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Core.EncodedVideoStreaming;
using FoosVision.Protocol.Connectivity.Configuration;
using FoosVision.Viewer.App.Screen.Stage;

namespace FoosVision.Viewer.App.Platforms.Android.Screen.Stage;

public class VideoPlayer : Java.Lang.Object, ISurfaceHolderCallback, IDisposable
{
    private const int _StreamWidth = 1920;
    private const int _StreamHeight = 1080;
    private const int _StreamFrameRate = 120;
    private static readonly Source _Log = new("Viewer.Android.VideoPlayer");
    private static readonly SourceInterval _RecoveryLog = new("Viewer.Android.VideoPlayer.LiveRecovery", TimeSpan.FromSeconds(1));
    private static readonly TimeSpan _StreamFpsWindow = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _StreamFpsPublishInterval = TimeSpan.FromMilliseconds(500);

    private readonly SurfaceView _SurfaceView;
    private readonly Lock _Gate = new();
    private readonly FrameRatePublisher _StreamFrameRatePublisher;
    private readonly Func<RuntimeMetricsOptions> _RuntimeMetricsOptionsProvider;
    private IntervalMetric? _ReceiveAccessUnitInterval;
    private IntervalMetric? _DecoderPushAccessUnitInterval;
    private IntervalMetric? _FrameRenderedInterval;
    private AndroidSurfaceVideoDecoder? _Decoder;
    private UdpClient? _UdpClient;
    private CancellationTokenSource? _PlaybackCts;
    private Task? _ReceiveTask;
    private PlaybackRequest _PlaybackRequest;
    private bool _PlaybackRequested;
    private bool _SurfaceAvailable;
    private int _ReplayPositionNotificationsEnabled;

    public VideoPlayer(
        SurfaceView surfaceView,
        Func<RuntimeMetricsOptions>? runtimeMetricsOptionsProvider = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        _SurfaceView = surfaceView;
        _RuntimeMetricsOptionsProvider = runtimeMetricsOptionsProvider ?? RuntimeMetricsOptions.CreateDefault;
        _StreamFrameRatePublisher = new FrameRatePublisher(_StreamFpsWindow, _StreamFpsPublishInterval, utcNow);
        _StreamFrameRatePublisher.FrameRateChanged += OnStreamFrameRateChanged;
        ISurfaceHolder holder = _SurfaceView.Holder
            ?? throw new InvalidOperationException("Video surface holder is not available.");
        holder.AddCallback(this);
    }

    public event Action<double?>? StreamFpsChanged;

    public event Func<Task>? ReplayLoopCompleted;

    public event Func<long, Task>? ReplayPositionChanged;

    public Task StartAsync(PlaybackRequest playbackRequest)
    {
        lock (_Gate)
        {
            _PlaybackRequest = playbackRequest;
            _PlaybackRequested = true;
            StartPlaybackIfReady();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        Task stopTask;
        lock (_Gate)
        {
            _PlaybackRequested = false;
            stopTask = StopPlayback();
        }

        return stopTask;
    }

    public void SurfaceCreated(ISurfaceHolder holder)
    {
        lock (_Gate)
        {
            _SurfaceAvailable = true;
            StartPlaybackIfReady();
        }
    }

    public void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height)
    {
        _Log.Information("Video surface changed: {0}x{1}", width, height);
    }

    public void SurfaceDestroyed(ISurfaceHolder holder)
    {
        lock (_Gate)
        {
            _SurfaceAvailable = false;
            _ = StopPlayback();
        }
    }

    public new void Dispose()
    {
        _SurfaceView.Holder?.RemoveCallback(this);
        lock (_Gate)
        {
            _PlaybackRequested = false;
            _ = StopPlayback();
        }

        _StreamFrameRatePublisher.FrameRateChanged -= OnStreamFrameRateChanged;
        _StreamFrameRatePublisher.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private void StartPlaybackIfReady()
    {
        if (!_PlaybackRequested || !_SurfaceAvailable || _ReceiveTask != null)
        {
            return;
        }

        Surface? surface = _SurfaceView.Holder?.Surface;
        if (surface == null || !surface.IsValid)
        {
            return;
        }

        _PlaybackCts = new CancellationTokenSource();
        _Decoder = new AndroidSurfaceVideoDecoder();
        _Decoder.FrameRendered += OnDecoderFrameRendered;
        Interlocked.Exchange(ref _ReplayPositionNotificationsEnabled, 0);
        _Decoder.Configure(
            new AndroidVideoDecoderOptions(
                MapCodec(_PlaybackRequest),
                _StreamWidth,
                _StreamHeight,
                EnableLowLatency: GetDecoderLowLatency(_PlaybackRequest)),
            surface);
        _StreamFrameRatePublisher.Reset();
        ConfigureRuntimeMetrics();

        CancellationToken token = _PlaybackCts.Token;

        switch (_PlaybackRequest.Kind)
        {
            case PlaybackKind.LiveStream:
                LiveVideoPlaybackOptions liveVideoOptions = GetLiveVideoOptions(_PlaybackRequest);
                LiveVideoPlaybackFrameOptions liveVideoFrameOptions = GetLiveVideoFrameOptions(liveVideoOptions, _StreamFrameRate);
                _UdpClient = new UdpClient(new IPEndPoint(IPAddress.Any, DefaultPorts.RtpH264StreamUdp))
                {
                    Client =
                    {
                        ReceiveBufferSize = liveVideoOptions.UdpReceiveBufferBytes,
                    },
                };

                _ReceiveTask = Task.Run(() => LiveStreamLoopAsync(_UdpClient, _Decoder, liveVideoFrameOptions, token), CancellationToken.None);

                _Log.Information(
                    "Started Android native RTP/H264 playback on UDP port {0}. BufferMs={1} BufferFrames={2} MaxBufferMs={3} MaxBufferFrames={4} ReceiveBufferBytes={5} DecoderLowLatency={6}",
                    DefaultPorts.RtpH264StreamUdp,
                    liveVideoOptions.PlaybackBufferMilliseconds,
                    liveVideoFrameOptions.PlaybackBufferFrames,
                    liveVideoOptions.MaxPlaybackBufferMilliseconds,
                    liveVideoFrameOptions.MaxPlaybackBufferFrames,
                    liveVideoOptions.UdpReceiveBufferBytes,
                    liveVideoOptions.DecoderLowLatency);
                return;

            case PlaybackKind.EncodedReplay:
                EncodedReplayPlayback replay = _PlaybackRequest.EncodedReplay
                ?? throw new InvalidOperationException("Encoded replay playback request is missing replay data.");

                Interlocked.Exchange(ref _ReplayPositionNotificationsEnabled, 1);
                _ReceiveTask = Task.Run(() => ReplayLoopAsync(replay, _Decoder, token), CancellationToken.None);

                _Log.Information(
                    "Started Android native replay playback. Codec={0} AccessUnits={1} Speed={2}",
                    replay.Codec,
                    replay.AccessUnits.Count,
                    replay.Speed);

                return;

            default:
                throw new NotSupportedException($"Playback kind '{_PlaybackRequest.Kind}' is not supported.");
        }
    }

    private Task StopPlayback()
    {
        CancellationTokenSource? cts = _PlaybackCts;
        UdpClient? udpClient = _UdpClient;
        Task? receiveTask = _ReceiveTask;
        AndroidSurfaceVideoDecoder? decoder = _Decoder;

        if (cts == null && udpClient == null && receiveTask == null && decoder == null)
        {
            return Task.CompletedTask;
        }

        _PlaybackCts = null;
        _UdpClient = null;
        _ReceiveTask = null;
        _Decoder = null;
        Interlocked.Exchange(ref _ReplayPositionNotificationsEnabled, 0);

        TryIgnore(() => cts?.Cancel(), "Cancel playback");
        TryIgnore(() => udpClient?.Close(), "Close UDP receiver");
        _StreamFrameRatePublisher.Reset();

        return Task.Run(() =>
        {
            TryIgnore(() => receiveTask?.Wait(TimeSpan.FromSeconds(1)), "Wait for receive loop");
            TryIgnore(() => udpClient?.Dispose(), "Dispose UDP receiver");
            TryIgnore(() => cts?.Dispose(), "Dispose cancellation source");
            TryIgnore(
                () =>
                {
                    if (decoder != null)
                    {
                        decoder.FrameRendered -= OnDecoderFrameRendered;
                    }
                },
                "Unsubscribe decoder frame release");
            TryIgnore(() => decoder?.Dispose(), "Dispose decoder");
            _Log.Information("Stopped Android native RTP/H264 playback.");
        });
    }

    private async Task LiveStreamLoopAsync(
        UdpClient udpClient,
        AndroidSurfaceVideoDecoder decoder,
        LiveVideoPlaybackFrameOptions options,
        CancellationToken cancellationToken)
    {
        using LiveAccessUnitQueue playbackQueue = new(options.PlaybackBufferFrames, options.MaxPlaybackBufferFrames);

        Task receiveTask = ReceiveLiveAccessUnitsAsync(udpClient, playbackQueue, cancellationToken);
        Task playbackTask = PlaybackLiveAccessUnitsAsync(decoder, playbackQueue, cancellationToken);

        try
        {
            await Task.WhenAll(receiveTask, playbackTask).ConfigureAwait(false);
        }
        finally
        {
            playbackQueue.Complete();
        }
    }

    private async Task ReceiveLiveAccessUnitsAsync(
        UdpClient udpClient,
        LiveAccessUnitQueue playbackQueue,
        CancellationToken cancellationToken)
    {
        RtpH264Depacketizer depacketizer = new();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await udpClient.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (!depacketizer.TryPushPacket(result.Buffer, out RtpH264AccessUnit accessUnit))
                {
                    continue;
                }

                _ReceiveAccessUnitInterval?.Record();
                _StreamFrameRatePublisher.RecordFrame();
                EnqueueLiveAccessUnit(playbackQueue, accessUnit);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException ex) when (cancellationToken.IsCancellationRequested)
            {
                _Log.Information("RTP receive loop stopped after socket cancellation: {0}", ex.SocketErrorCode);
                return;
            }
            catch (Exception ex)
            {
                _Log.Warning("RTP receive failed: {0}", ex);
                depacketizer.Reset();
                playbackQueue.RequestKeyFrameRecovery();
                _StreamFrameRatePublisher.Reset();
            }
        }
    }

    private async Task PlaybackLiveAccessUnitsAsync(
        AndroidSurfaceVideoDecoder decoder,
        LiveAccessUnitQueue playbackQueue,
        CancellationToken cancellationToken)
    {
        PlaybackClock playbackClock = new();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!playbackQueue.TryDequeue(cancellationToken, out RtpH264AccessUnit accessUnit, out bool resetDecoder))
                {
                    return;
                }

                if (resetDecoder)
                {
                    playbackClock.Reset();
                    decoder.Reset();
                }

                await playbackClock.DelayUntilDueAsync(accessUnit.TimeNs, cancellationToken).ConfigureAwait(false);
                _DecoderPushAccessUnitInterval?.Record();
                decoder.PushAccessUnit(
                    accessUnit.Buffer,
                    accessUnit.TimeNs,
                    accessUnit.IsKeyFrame);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _Log.Warning("RTP playback/decode failed: {0}", ex);
                playbackClock.Reset();
                decoder.Reset();
                playbackQueue.RequestKeyFrameRecovery();
                _StreamFrameRatePublisher.Reset();
            }
        }
    }

    private static void EnqueueLiveAccessUnit(
        LiveAccessUnitQueue playbackQueue,
        RtpH264AccessUnit accessUnit)
    {
        LiveAccessUnitQueueResult result = playbackQueue.Enqueue(accessUnit);

        if (result.DroppedAccessUnits > 0)
        {
            _RecoveryLog.Warning(
                "Live playback backlog: dropped {0} access units; waiting for next keyframe={1}",
                result.DroppedAccessUnits,
                result.WaitingForKeyFrame);
        }
    }

    private void ConfigureRuntimeMetrics()
    {
        RuntimeMetricsOptions options = _RuntimeMetricsOptionsProvider();

        if (!options.Enabled)
        {
            _ReceiveAccessUnitInterval = null;
            _DecoderPushAccessUnitInterval = null;
            _FrameRenderedInterval = null;
            return;
        }

        TimeSpan reportInterval = options.GetReportInterval();

        _ReceiveAccessUnitInterval = new IntervalMetric(
            options.CreateMetricName("Viewer.RtpH264Receiver.AccessUnitReceiveInterval"),
            _Log,
            reportInterval);
        _DecoderPushAccessUnitInterval = new IntervalMetric(
            options.CreateMetricName("Viewer.H264Decoder.AccessUnitPushInterval"),
            _Log,
            reportInterval);
        _FrameRenderedInterval = new IntervalMetric(
            options.CreateMetricName("Viewer.H264Decoder.FrameRenderedInterval"),
            _Log,
            reportInterval);
    }

    private async Task ReplayLoopAsync(
        EncodedReplayPlayback replay,
        AndroidSurfaceVideoDecoder decoder,
        CancellationToken cancellationToken)
    {
        if (replay.AccessUnits.Count == 0)
        {
            _Log.Warning("Replay playback skipped because the replay contains no access units.");
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            PlaybackClock playbackClock = new();
            decoder.Reset();

            try
            {
                foreach (PlaybackAccessUnit accessUnit in replay.AccessUnits)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await playbackClock.DelayUntilDueAsync(accessUnit.TimeNs, replay.Speed, cancellationToken).ConfigureAwait(false);
                    decoder.PushAccessUnit(
                        PrepareReplayAccessUnit(replay, accessUnit),
                        accessUnit.TimeNs,
                        accessUnit.IsKeyFrame);
                    _StreamFrameRatePublisher.RecordFrame();
                }

                await NotifyReplayLoopCompletedAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _Log.Warning("Replay decode loop failed: {0}", ex);
                decoder.Reset();
                _StreamFrameRatePublisher.Reset();
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task NotifyReplayLoopCompletedAsync()
    {
        Func<Task>? replayLoopCompleted = ReplayLoopCompleted;
        if (replayLoopCompleted == null)
        {
            return;
        }

        try
        {
            await replayLoopCompleted().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _Log.Warning("Replay loop completion callback failed: {0}", ex);
        }
    }

    private async Task NotifyReplayPositionChangedAsync(long timeNs)
    {
        Func<long, Task>? replayPositionChanged = ReplayPositionChanged;

        if (replayPositionChanged == null) return;

        try
        {
            await replayPositionChanged(timeNs).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _Log.Warning("Replay position callback failed: {0}", ex);
        }
    }

    private void OnDecoderFrameRendered(object? sender, FrameRenderedEventArgs e)
    {
        _FrameRenderedInterval?.Record();

        if (Interlocked.CompareExchange(ref _ReplayPositionNotificationsEnabled, 0, 0) == 0)
        {
            return;
        }

        if (!ReferenceEquals(sender, _Decoder))
        {
            return;
        }

        _ = NotifyReplayPositionChangedAsync(e.TimeNs);
    }

    private static CodecType MapCodec(PlaybackRequest playbackRequest)
    {
        if (playbackRequest.Kind == PlaybackKind.LiveStream)
        {
            return CodecType.H264;
        }

        return playbackRequest.EncodedReplay?.Codec switch
        {
            PlaybackCodec.H264 => CodecType.H264,
            PlaybackCodec.H265 => CodecType.H265,
            _ => throw new NotSupportedException($"Replay codec '{playbackRequest.EncodedReplay?.Codec}' is not supported."),
        };
    }

    private static LiveVideoPlaybackOptions GetLiveVideoOptions(PlaybackRequest playbackRequest)
    {
        return playbackRequest.LiveVideo ?? LiveVideoPlaybackOptions.Default;
    }

    private static LiveVideoPlaybackFrameOptions GetLiveVideoFrameOptions(LiveVideoPlaybackOptions options, int streamFrameRate)
    {
        int playbackBufferFrames = GetFrameCountForMilliseconds(options.PlaybackBufferMilliseconds, streamFrameRate);
        int maxPlaybackBufferFrames = Math.Max(
            playbackBufferFrames + 1,
            GetFrameCountForMilliseconds(options.MaxPlaybackBufferMilliseconds, streamFrameRate));

        return new LiveVideoPlaybackFrameOptions(playbackBufferFrames, maxPlaybackBufferFrames);
    }

    private static int GetFrameCountForMilliseconds(int milliseconds, int streamFrameRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(streamFrameRate);

        return (int)Math.Ceiling(milliseconds * streamFrameRate / 1000.0);
    }

    private static bool GetDecoderLowLatency(PlaybackRequest playbackRequest)
    {
        return playbackRequest.Kind != PlaybackKind.LiveStream ||
            GetLiveVideoOptions(playbackRequest).DecoderLowLatency;
    }

    private static byte[] PrepareReplayAccessUnit(EncodedReplayPlayback replay, PlaybackAccessUnit accessUnit)
    {
        if (!accessUnit.IsKeyFrame || accessUnit.ContainsAllRequiredParameterSets || replay.ParameterSets.Count == 0)
        {
            return accessUnit.Buffer;
        }

        IReadOnlyList<PlaybackParameterSetType> requiredTypes = replay.Codec == PlaybackCodec.H264
            ? [PlaybackParameterSetType.SPS, PlaybackParameterSetType.PPS]
            : [PlaybackParameterSetType.VPS, PlaybackParameterSetType.SPS, PlaybackParameterSetType.PPS];

        List<byte[]> parameterSetBuffers = [];
        foreach (PlaybackParameterSetType requiredType in requiredTypes)
        {
            PlaybackParameterSet parameterSet = replay.ParameterSets.LastOrDefault(p => p.Type == requiredType);
            if (parameterSet.Buffer is { Length: > 0 })
            {
                parameterSetBuffers.Add(parameterSet.Buffer);
            }
        }

        if (parameterSetBuffers.Count == 0)
        {
            return accessUnit.Buffer;
        }

        int length = accessUnit.Buffer.Length + parameterSetBuffers.Sum(p => p.Length);
        byte[] buffer = new byte[length];
        int offset = 0;
        foreach (byte[] parameterSet in parameterSetBuffers)
        {
            parameterSet.CopyTo(buffer, offset);
            offset += parameterSet.Length;
        }

        accessUnit.Buffer.CopyTo(buffer, offset);
        return buffer;
    }

    private void OnStreamFrameRateChanged(double? frameRate)
    {
        StreamFpsChanged?.Invoke(frameRate);
    }

    private static void TryIgnore(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _Log.Warning("{0} failed: {1}", what, ex);
        }
    }

    private readonly record struct LiveAccessUnitQueueResult(
        int DroppedAccessUnits,
        bool WaitingForKeyFrame);

    private readonly record struct LiveVideoPlaybackFrameOptions(
        int PlaybackBufferFrames,
        int MaxPlaybackBufferFrames);

    private sealed class LiveAccessUnitQueue : IDisposable
    {
        private readonly Lock _Gate = new();
        private readonly Queue<RtpH264AccessUnit> _Queue = [];
        private readonly AutoResetEvent _Available = new(false);
        private readonly int _PlaybackBufferFrameCount;
        private readonly int _MaxBufferFrameCount;
        private bool _Completed;
        private bool _WaitingForKeyFrame;
        private bool _ResetDecoderOnNextFrame;

        public LiveAccessUnitQueue(int playbackBufferFrameCount, int maxBufferFrameCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(playbackBufferFrameCount);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxBufferFrameCount, playbackBufferFrameCount);

            _PlaybackBufferFrameCount = playbackBufferFrameCount;
            _MaxBufferFrameCount = maxBufferFrameCount;
        }

        public LiveAccessUnitQueueResult Enqueue(RtpH264AccessUnit accessUnit)
        {
            int droppedAccessUnits = 0;
            bool signal = false;
            bool waitingForKeyFrame;

            lock (_Gate)
            {
                if (_Completed)
                {
                    return new LiveAccessUnitQueueResult(0, _WaitingForKeyFrame);
                }

                if (_WaitingForKeyFrame && !accessUnit.IsKeyFrame)
                {
                    return new LiveAccessUnitQueueResult(0, true);
                }

                if (_WaitingForKeyFrame)
                {
                    _WaitingForKeyFrame = false;
                    _ResetDecoderOnNextFrame = true;
                }

                _Queue.Enqueue(accessUnit);
                signal = _Queue.Count > _PlaybackBufferFrameCount;

                if (_Queue.Count > _MaxBufferFrameCount)
                {
                    droppedAccessUnits = _Queue.Count;
                    _Queue.Clear();
                    _ResetDecoderOnNextFrame = true;

                    if (accessUnit.IsKeyFrame)
                    {
                        _WaitingForKeyFrame = false;
                        _Queue.Enqueue(accessUnit);
                        signal = _Queue.Count > _PlaybackBufferFrameCount;
                    }
                    else
                    {
                        _WaitingForKeyFrame = true;
                        signal = false;
                    }
                }

                waitingForKeyFrame = _WaitingForKeyFrame;
            }

            if (signal)
            {
                _Available.Set();
            }

            return new LiveAccessUnitQueueResult(droppedAccessUnits, waitingForKeyFrame);
        }

        public bool TryDequeue(
            CancellationToken cancellationToken,
            out RtpH264AccessUnit accessUnit,
            out bool resetDecoder)
        {
            while (true)
            {
                lock (_Gate)
                {
                    if (_Queue.Count > _PlaybackBufferFrameCount)
                    {
                        accessUnit = _Queue.Dequeue();
                        resetDecoder = _ResetDecoderOnNextFrame;
                        _ResetDecoderOnNextFrame = false;

                        if (_Queue.Count > _PlaybackBufferFrameCount)
                        {
                            _Available.Set();
                        }

                        return true;
                    }

                    if (_Completed)
                    {
                        accessUnit = default;
                        resetDecoder = false;
                        return false;
                    }
                }

                int waitResult = WaitHandle.WaitAny([_Available, cancellationToken.WaitHandle]);

                if (waitResult == 1)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }
        }

        public void RequestKeyFrameRecovery()
        {
            lock (_Gate)
            {
                _Queue.Clear();
                _WaitingForKeyFrame = true;
                _ResetDecoderOnNextFrame = true;
            }
        }

        public void Complete()
        {
            lock (_Gate)
            {
                _Completed = true;
            }

            _Available.Set();
        }

        public void Dispose()
        {
            _Available.Dispose();
        }
    }
}
