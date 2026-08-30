// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics;
using System.Threading.Channels;
using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Adapters.Viewer.Session.Playback;
using FoosVision.Adapters.Viewer.Session.Replay;
using FoosVision.Common.Logging;
using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Entities;
using FoosVision.Domain.Replay.ValueObjects;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Messages.Live;
using FoosVision.Protocol.Messages.LiveAnalysis;
using FoosVision.UseCases.Replay.CompleteReplayAnalysis;
using FoosVision.UseCases.Replay.CompleteReplayLoop;
using FoosVision.UseCases.Replay.ObserveLiveTracking;
using FoosVision.UseCases.Replay.Ports;
using FoosVision.UseCases.Replay.ProcessReplayFrame;
using FoosVision.UseCases.Replay.StartReplayAnalysis;
using FoosVision.UseCases.Replay.StopReplay;

namespace FoosVision.Adapters.Viewer.Session.Active;

internal class ViewerReplayCoordinator :
    IStartReplayAnalysisOutputPort,
    IProcessReplayFrameOutputPort,
    ICompleteReplayAnalysisOutputPort,
    ICompleteReplayLoopOutputPort,
    IObserveLiveTrackingOutputPort,
    IStopReplayOutputPort,
    IDisposable
{
    private const int _ReplayAnalysisFrameQueueCapacity = 16;

    private static readonly Source _Log = new("Viewer.Session.Active.ViewerReplayCoordinator");

    private readonly Lock _AnalysisSync = new();
    private readonly Lock _ReplacementSync = new();
    private readonly IOverlaySink _OverlaySink;
    private readonly ViewerPlaybackCoordinator _PlaybackCoordinator;
    private readonly IReplaySessionStore _ReplaySessionStore;
    private readonly IBallFinder _BallFinder;
    private readonly IEncodedReplayFrameDecoder _ReplayFrameDecoder;
    private readonly Func<Option<TableConfiguration>> _GetLatestTableConfiguration;
    private readonly Func<bool> _HasVisionContext;
    private readonly Action _ResetTrackingOverlay;
    private readonly Func<Task> _StartLivePlayback;
    private readonly Action<double?> _UpdateTrackingFps;
    private readonly StartReplayAnalysisInteractor _StartReplayAnalysis;
    private readonly ProcessReplayFrameInteractor _ProcessReplayFrame;
    private readonly CompleteReplayAnalysisInteractor _CompleteReplayAnalysis;
    private readonly CompleteReplayLoopInteractor _CompleteReplayLoop;
    private readonly ObserveLiveTrackingInteractor _ObserveLiveTracking;
    private readonly StopReplayInteractor _StopReplay;
    private ReplayAnalysis? _ReplayAnalysis;
    private ReplayPossessionOverlay? _ReplayPossessionOverlay;
    private CancellationTokenSource? _ReplayReplacementCts;
    private long _ReplayGeneration;
    private int _ReplayPending;

    public ViewerReplayCoordinator(
        IOverlaySink overlaySink,
        ViewerPlaybackCoordinator playbackCoordinator,
        IReplaySessionStore replaySessionStore,
        IBallFinder ballFinder,
        IEncodedReplayFrameDecoder replayFrameDecoder,
        Func<Option<TableConfiguration>> getLatestTableConfiguration,
        Func<bool> hasVisionContext,
        Action resetTrackingOverlay,
        Func<Task> startLivePlayback,
        Action<double?> updateTrackingFps)
    {
        _OverlaySink = overlaySink;
        _PlaybackCoordinator = playbackCoordinator;
        _ReplaySessionStore = replaySessionStore;
        _BallFinder = ballFinder;
        _ReplayFrameDecoder = replayFrameDecoder;
        _GetLatestTableConfiguration = getLatestTableConfiguration;
        _HasVisionContext = hasVisionContext;
        _ResetTrackingOverlay = resetTrackingOverlay;
        _StartLivePlayback = startLivePlayback;
        _UpdateTrackingFps = updateTrackingFps;
        _StartReplayAnalysis = new StartReplayAnalysisInteractor(_ReplaySessionStore);
        _ProcessReplayFrame = new ProcessReplayFrameInteractor(_ReplaySessionStore);
        _CompleteReplayAnalysis = new CompleteReplayAnalysisInteractor(_ReplaySessionStore);
        _CompleteReplayLoop = new CompleteReplayLoopInteractor(_ReplaySessionStore);
        _ObserveLiveTracking = new ObserveLiveTrackingInteractor(_ReplaySessionStore);
        _StopReplay = new StopReplayInteractor(_ReplaySessionStore);
        _PlaybackCoordinator.ReplayLoopCompleted += OnReplayLoopCompleted;
        _PlaybackCoordinator.ReplayPositionChanged += OnReplayPositionChanged;
    }

    public bool IsReplayPending => Interlocked.CompareExchange(ref _ReplayPending, 0, 0) != 0;

    public bool IsReplayActive => IsReplayPending || _ReplaySessionStore.HasActive;

    public void HandleReplayStarted(ReplayStartedMessage message)
    {
        ReplayReplacement replacement = BeginReplayReplacement();
        _ = PreparePendingReplayAsync(replacement);
    }

    public void HandleReplay(ReplayMessage message)
    {
        ReplayReplacement replacement = BeginReplayReplacement();
        _ = StartReplayAsync(message, replacement);
    }

    public Task ObserveLiveTrackingAsync(Point? liveBallPosition)
    {
        return _ObserveLiveTracking.Handle(
            new ObserveLiveTrackingRequest(liveBallPosition),
            this,
            CancellationToken.None);
    }

    public async Task StopReplayAsync()
    {
        ClearReplayPending();

        try
        {
            await _StopReplay.Handle(new StopReplayRequest(), this, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _Log.Warning("Replay stop failed: {0}", ex);
        }
    }

    public void ResetAnalysis()
    {
        lock (_AnalysisSync)
        {
            _ReplayAnalysis = null;
            _ReplayPossessionOverlay = null;
        }
    }

    public void CancelReplayReplacement()
    {
        CancellationTokenSource? cancellation;

        lock (_ReplacementSync)
        {
            cancellation = _ReplayReplacementCts;
            _ReplayReplacementCts = null;
            _ReplayGeneration++;
        }

        cancellation?.Cancel();
    }

    public Task ReportReplayAnalysisStarted(ReplayAnalysisStartedResponse response)
    {
        _Log.Information("Replay analysis started. TriggerFrameId={0} TriggerTimestampNs={1}", response.ReplayId.TriggerFrameId, response.ReplayId.TriggerTimestampNs);
        return Task.CompletedTask;
    }

    public Task ReportReplayFrameProcessed(ReplayFrameProcessedResponse response)
    {
        return Task.CompletedTask;
    }

    public Task ReportReplayAnalysisCompleted(ReplayAnalysisCompletedResponse response)
    {
        lock (_AnalysisSync)
        {
            _ReplayAnalysis = response.Analysis;
        }

        return Task.CompletedTask;
    }

    public async Task ReportReturnToLive(ReturnToLiveResponse response)
    {
        ClearReplayPending();
        _Log.Information("Replay returned to live. TriggerFrameId={0} TriggerTimestampNs={1}", response.ReplayId.TriggerFrameId, response.ReplayId.TriggerTimestampNs);
        await _StartLivePlayback();
    }

    public Task ReportStopped(ReplayStoppedResponse response)
    {
        _UpdateTrackingFps(null);
        _Log.Information("Replay stopped. TriggerFrameId={0} TriggerTimestampNs={1}", response.ReplayId.TriggerFrameId, response.ReplayId.TriggerTimestampNs);
        return Task.CompletedTask;
    }

    public Task ReportStopFailed(string reason)
    {
        _Log.Information("Replay stop skipped. Reason={0}", reason);
        return Task.CompletedTask;
    }

    public Task ReportSkipped(string reason)
    {
        _Log.Information("Replay operation skipped. Reason={0}", reason);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _PlaybackCoordinator.ReplayLoopCompleted -= OnReplayLoopCompleted;
        _PlaybackCoordinator.ReplayPositionChanged -= OnReplayPositionChanged;
        CancelReplayReplacement();
    }

    private Task OnReplayLoopCompleted()
    {
        if (IsReplayPending)
        {
            return Task.CompletedTask;
        }

        return _CompleteReplayLoop.Handle(new CompleteReplayLoopRequest(), this, CancellationToken.None);
    }

    private Task OnReplayPositionChanged(long timeNs)
    {
        if (IsReplayPending)
        {
            return Task.CompletedTask;
        }

        ReplayAnalysis? analysis;
        ReplayPossessionOverlay? possessionOverlay;
        lock (_AnalysisSync)
        {
            analysis = _ReplayAnalysis;
            possessionOverlay = _ReplayPossessionOverlay;
        }

        if (analysis is null)
        {
            return Task.CompletedTask;
        }

        TrackingOverlayState state = ReplayAnalysisMapper.Map(analysis, timeNs, possessionOverlay);
        _OverlaySink.UpdateTrackingState(state);
        return Task.CompletedTask;
    }

    private async Task StartReplayAsync(ReplayMessage message, ReplayReplacement replacement)
    {
        CancellationToken ct = replacement.Cancellation.Token;

        try
        {
            if (!_GetLatestTableConfiguration().TryGetValue(out TableConfiguration tableConfiguration))
            {
                ClearReplayPendingIfCurrent(replacement.Generation);
                _Log.Warning("Replay ignored because no table configuration is available.");
                return;
            }

            if (!_HasVisionContext())
            {
                ClearReplayPendingIfCurrent(replacement.Generation);
                _Log.Warning("Replay ignored because no vision context has been applied.");
                return;
            }

            if (!ReplayPlaybackRequestMapper.TryMap(message, out PlaybackRequest playbackRequest, out string rejectionReason))
            {
                ClearReplayPendingIfCurrent(replacement.Generation);
                _Log.Warning("Replay rejected. Reason={0}", rejectionReason);
                return;
            }

            MarkReplayPending();
            _ResetTrackingOverlay();
            _ReplaySessionStore.Clear();
            await _PlaybackCoordinator.StopAsync(ct);

            if (!IsCurrentReplayGeneration(replacement.Generation))
            {
                return;
            }

            ReplayId replayId = new(message.TriggerFrameId, message.TriggerTimestampNs);
            Point anchorPosition = new(message.AnchorPosition.X, message.AnchorPosition.Y);
            ReplayTrackAnchor trackAnchor = new(
                new Frame(message.AnchorFrameId, message.AnchorTimestampNs),
                anchorPosition);
            ReplayPossessionOverlay possessionOverlay = new(
                ParsePossession(message.AnchorPossession),
                message.AnchorTimestampNs,
                message.AnchorPossessionTimeMs);
            StartReplayAnalysisRequest request = new(
                replayId,
                trackAnchor,
                tableConfiguration);

            EncodedReplayPlayback replay = playbackRequest.EncodedReplay
                ?? throw new InvalidOperationException("Replay mapping did not produce replay playback data.");
            EncodedReplayFrameSource frameSource = new(_ReplayFrameDecoder, replay);

            await _StartReplayAnalysis.Handle(request, this, ct);
            lock (_AnalysisSync)
            {
                _ReplayPossessionOverlay = possessionOverlay;
            }

            long replayAnalysisStartTime = Stopwatch.GetTimestamp();
            TimeSpan decodeElapsed = TimeSpan.Zero;
            TimeSpan analyzeElapsed = TimeSpan.Zero;
            int decodedFrameCount = 0;
            Channel<ReplayFrame> replayFrameQueue = Channel.CreateBounded<ReplayFrame>(
                new BoundedChannelOptions(_ReplayAnalysisFrameQueueCapacity)
                {
                    SingleReader = true,
                    SingleWriter = true,
                    FullMode = BoundedChannelFullMode.Wait,
                });
            using CancellationTokenSource replayFrameProducerCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Task replayFrameProducer = ProduceReplayFramesAsync(replayFrameQueue.Writer, replayFrameProducerCancellation.Token);

            try
            {
                await foreach (ReplayFrame frame in replayFrameQueue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                {
                    ct.ThrowIfCancellationRequested();

                    if (!IsCurrentReplayGeneration(replacement.Generation))
                    {
                        frame.Frame.Release();
                        return;
                    }

                    if (frame.Frame.Meta.TimestampNs <= message.AnchorTimestampNs)
                    {
                        frame.Frame.Release();
                        continue;
                    }

                    Frame replayFrame = new((ulong)_ReplaySessionStore.LoadActive().Value.TrackedFrameCount, frame.Frame.Meta.TimestampNs);

                    long analyzeStartTime = Stopwatch.GetTimestamp();
                    try
                    {
                        ReplayVisionOps visionOps = new(_BallFinder, frame);
                        await _ProcessReplayFrame.Handle(new ProcessReplayFrameRequest(replayFrame, visionOps), this, ct);
                    }
                    finally
                    {
                        analyzeElapsed += Stopwatch.GetElapsedTime(analyzeStartTime);
                        frame.Frame.Release();
                    }
                }

                await replayFrameProducer.ConfigureAwait(false);
            }
            catch
            {
                replayFrameProducerCancellation.Cancel();
                replayFrameQueue.Writer.TryComplete();

                try
                {
                    await replayFrameProducer.ConfigureAwait(false);
                }
                catch
                {
                }

                throw;
            }

            if (!IsCurrentReplayGeneration(replacement.Generation))
            {
                return;
            }

            await _CompleteReplayAnalysis.Handle(new CompleteReplayAnalysisRequest(), this, ct);
            TimeSpan replayAnalysisElapsed = Stopwatch.GetElapsedTime(replayAnalysisStartTime);
            _Log.Information(
                "Replay analysis timings. DecodedFrames={0} DecodeMs={1:0.0} AnalyzeMs={2:0.0} TotalMs={3:0.0}",
                decodedFrameCount,
                decodeElapsed.TotalMilliseconds,
                analyzeElapsed.TotalMilliseconds,
                replayAnalysisElapsed.TotalMilliseconds);

            await _PlaybackCoordinator.StartReplayAsync(
                playbackRequest,
                () => IsCurrentReplayGeneration(replacement.Generation),
                ClearReplayPending,
                ct);

            async Task ProduceReplayFramesAsync(ChannelWriter<ReplayFrame> writer, CancellationToken ct)
            {
                IAsyncEnumerator<ReplayFrame> frameEnumerator = frameSource.ReadFrames(ct)
                    .GetAsyncEnumerator(ct);

                try
                {
                    while (true)
                    {
                        long decodeStartTime = Stopwatch.GetTimestamp();
                        bool hasFrame = await frameEnumerator.MoveNextAsync().ConfigureAwait(false);
                        decodeElapsed += Stopwatch.GetElapsedTime(decodeStartTime);

                        if (!hasFrame)
                        {
                            writer.TryComplete();
                            return;
                        }

                        decodedFrameCount++;
                        ReplayFrame frame = frameEnumerator.Current;
                        bool handedOff = false;

                        try
                        {
                            await writer.WriteAsync(frame, ct).ConfigureAwait(false);
                            handedOff = true;
                        }
                        finally
                        {
                            if (!handedOff)
                            {
                                frame.Frame.Release();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    writer.TryComplete(ex);
                    throw;
                }
                finally
                {
                    await frameEnumerator.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ClearReplayPendingIfCurrent(replacement.Generation);
            _Log.Warning("Replay start failed: {0}", ex);
        }
        finally
        {
            CompleteReplayReplacement(replacement);
        }
    }

    private async Task PreparePendingReplayAsync(ReplayReplacement replacement)
    {
        CancellationToken ct = replacement.Cancellation.Token;

        try
        {
            MarkReplayPending();
            _ResetTrackingOverlay();
            _ReplaySessionStore.Clear();
            await _PlaybackCoordinator.StopAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            CompleteReplayReplacement(replacement);
        }
    }

    private void MarkReplayPending()
    {
        Interlocked.Exchange(ref _ReplayPending, 1);
        _UpdateTrackingFps(null);
    }

    private void ClearReplayPending()
    {
        Interlocked.Exchange(ref _ReplayPending, 0);
        _UpdateTrackingFps(null);
    }

    private void ClearReplayPendingIfCurrent(long replayGeneration)
    {
        if (IsCurrentReplayGeneration(replayGeneration))
        {
            ClearReplayPending();
        }
    }

    private ReplayReplacement BeginReplayReplacement()
    {
        CancellationTokenSource cancellation = new();
        CancellationTokenSource? previousCancellation;
        long generation;

        lock (_ReplacementSync)
        {
            previousCancellation = _ReplayReplacementCts;
            _ReplayReplacementCts = cancellation;
            generation = ++_ReplayGeneration;
        }

        previousCancellation?.Cancel();

        return new ReplayReplacement(generation, cancellation);
    }

    private void CompleteReplayReplacement(ReplayReplacement replacement)
    {
        lock (_ReplacementSync)
        {
            if (ReferenceEquals(_ReplayReplacementCts, replacement.Cancellation))
            {
                _ReplayReplacementCts = null;
            }
        }

        replacement.Cancellation.Dispose();
    }

    private bool IsCurrentReplayGeneration(long replayGeneration)
        => Interlocked.Read(ref _ReplayGeneration) == replayGeneration;

    private static BallPossession ParsePossession(PossessionMessage value)
    {
        Team team = value.Team switch
        {
            TeamMessage.A => Team.A,
            TeamMessage.B => Team.B,
            _ => Team.None,
        };

        PossessionArea area = value.Area switch
        {
            PossessionAreaMessage.Defense => PossessionArea.Defense,
            PossessionAreaMessage.FiveBar => PossessionArea.FiveBar,
            PossessionAreaMessage.ThreeBar => PossessionArea.ThreeBar,
            _ => PossessionArea.None,
        };

        return new BallPossession(team, area);
    }

    private readonly record struct ReplayReplacement(
        long Generation,
        CancellationTokenSource Cancellation);
}
