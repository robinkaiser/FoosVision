// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Threading.Channels;
using FoosVision.Common.Logging;
using FoosVision.Common.Types;
using FoosVision.Domain.Replay.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Ports.Media;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Live;
using FoosVision.Protocol.Messages.LiveAnalysis;

namespace FoosVision.Adapters.Recorder.Replay;

public class ReplayCoordinator : IReplayCoordinator, IDisposable
{
    private const int _TailCheckIntervalMs = 20;
    private const int _MaxTailChecks = 50;

    private static readonly Source _Log = new("ReplayCoordinator");

    private readonly IEncodedReplayBuffer _ReplayBuffer;
    private readonly ILiveVideoStreamController _LiveVideoStreamController;
    private readonly IRecorderLiveAnalysisPublisher _LiveAnalysisPublisher;
    private readonly Channel<ReplayRequest> _Requests;
    private readonly CancellationTokenSource _Cts = new();
    private readonly Task _Worker;
    private readonly Lock _RequestGate = new();
    private ReplayRequest? _AcceptedReplay;
    private readonly TimeProvider _TimeProvider;
    private Guid? _SessionId;
    private CancellationTokenSource? _SessionCancellation;

    public ReplayCoordinator(
        IEncodedReplayBuffer replayBuffer,
        ILiveVideoStreamController liveVideoStreamController,
        IRecorderLiveAnalysisPublisher liveAnalysisPublisher,
        TaskScheduler? workerScheduler = null,
        TimeProvider? timeProvider = null)
    {
        _TimeProvider = timeProvider ?? TimeProvider.System;
        _ReplayBuffer = replayBuffer;
        _LiveVideoStreamController = liveVideoStreamController;
        _LiveAnalysisPublisher = liveAnalysisPublisher;
        _Requests = Channel.CreateBounded<ReplayRequest>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        _Worker = Task.Factory.StartNew(
            () => ProcessRequests(_Cts.Token),
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            workerScheduler ?? TaskScheduler.Default).Unwrap();
    }

    public void StartSession(Guid sessionId)
    {
        lock (_RequestGate)
        {
            _SessionCancellation?.Cancel();
            _SessionCancellation?.Dispose();
            _SessionCancellation = new();
            _SessionId = sessionId;
            _AcceptedReplay = null;
        }
    }

    public void StopSession(Guid sessionId)
    {
        lock (_RequestGate)
        {
            if (_SessionId != sessionId)
            {
                return;
            }

            _SessionCancellation?.Cancel();
            _SessionCancellation?.Dispose();
            _SessionCancellation = null;
            _SessionId = null;
            _AcceptedReplay = null;
        }
    }

    public Task RequestReplay(Guid sessionId, Frame triggerFrame, ReplayPlan replay)
    {
        lock (_RequestGate)
        {
            if (_SessionId != sessionId || _SessionCancellation is null)
            {
                return Task.CompletedTask;
            }

            ReplayRequest request = new(sessionId, triggerFrame, replay, _SessionCancellation.Token);
            if (!_Requests.Writer.TryWrite(request))
            {
                _Log.Warning("Request skipped, the worker send queue is closed. TriggerFrameId={FrameId}", triggerFrame.Id);
                return Task.CompletedTask;
            }

            _AcceptedReplay = request;
        }

        _Log.Information(
            "Request accepted. TriggerFrameId={TriggerFrameId} AnchorFrameId={AnchorFrameId} AnchorTimestampNs={AnchorTimestampNs} AnchorPosition=({X},{Y}) ReplayStartNs={ReplayStartNs} ReplayEndNs={ReplayEndNs}",
            triggerFrame.Id,
            replay.AnchorFrame.Id,
            replay.AnchorFrame.TimestampNs,
            replay.AnchorPosition.X,
            replay.AnchorPosition.Y,
            replay.StartTimestampNs,
            replay.EndTimestampNs);
        return Task.CompletedTask;
    }

    public Task RequestReturnToLive(Guid sessionId, Frame triggerFrame)
    {
        lock (_RequestGate)
        {
            if (_AcceptedReplay is not ReplayRequest accepted || accepted.SessionId != sessionId ||
                triggerFrame != accepted.TriggerFrame)
            {
                return Task.CompletedTask;
            }
        }

        return _LiveAnalysisPublisher.PublishReplayReturnToLive(new ReplayReturnToLiveMessage
        {
            SessionId = sessionId,
            TriggerFrameId = triggerFrame.Id,
            TriggerTimestampNs = triggerFrame.TimestampNs,
        }, _Cts.Token);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _Cts.Cancel();
        _Requests.Writer.TryComplete();

        try
        {
            _Worker.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
        }

        _SessionCancellation?.Dispose();
        _Cts.Dispose();
    }

    private async Task ProcessRequests(CancellationToken ct)
    {
        try
        {
            await foreach (ReplayRequest request in _Requests.Reader.ReadAllAsync(ct))
            {
                try
                {
                    using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, request.SessionCancellation);
                    cancellation.Token.ThrowIfCancellationRequested();
                    await ProcessRequest(request, cancellation.Token);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (OperationCanceledException) when (request.SessionCancellation.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    _Log.Error("Request failed. TriggerFrameId={FrameId} Ex={Exception}", request.TriggerFrame.Id, ex.ToString());
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task ProcessRequest(ReplayRequest request, CancellationToken ct)
    {
        ReplayPlan replay = request.Replay;
        EncodedReplaySegment? segment = await GetReplaySegment(replay, ct);
        if (segment is null)
        {
            _Log.Warning(
                "Replay segment unavailable. TriggerFrameId={TriggerFrameId} AnchorFrameId={AnchorFrameId}",
                request.TriggerFrame.Id, replay.AnchorFrame.Id);
            return;
        }

        ct.ThrowIfCancellationRequested();
        int accessUnitBytes = segment.AccessUnits.Sum(accessUnit => accessUnit.Buffer.Length);

        ReplayStartedMessage startedMessage = new()
        {
            SessionId = request.SessionId,
            TriggerFrameId = request.TriggerFrame.Id,
            TriggerTimestampNs = request.TriggerFrame.TimestampNs,
            AnchorFrameId = replay.AnchorFrame.Id,
            AnchorTimestampNs = replay.AnchorFrame.TimestampNs,
            AnchorPosition = CreatePosition(replay.AnchorPosition),
            AnchorPossession = CreatePossession(replay.AnchorPossession),
            AnchorPossessionTimeMs = replay.AnchorPossessionTimeMs,
            ReplayStartTimestampNs = segment.StartTimeNs,
            ReplayEndTimestampNs = segment.EndTimeNs,
            Speed = replay.Speed,
            RequiredCompletedLoops = replay.RequiredCompletedLoops,
            Codec = CreateCodec(segment.Codec),
            ParameterSetCount = segment.ParameterSets.Count,
            AccessUnitCount = segment.AccessUnits.Count,
            AccessUnitBytes = accessUnitBytes,
        };

        await _LiveAnalysisPublisher.PublishReplayStarted(startedMessage, ct);

        ReplayClipMessage message = new()
        {
            SessionId = request.SessionId,
            TriggerFrameId = request.TriggerFrame.Id,
            TriggerTimestampNs = request.TriggerFrame.TimestampNs,
            AnchorFrameId = replay.AnchorFrame.Id,
            AnchorTimestampNs = replay.AnchorFrame.TimestampNs,
            AnchorPosition = CreatePosition(replay.AnchorPosition),
            AnchorPossession = CreatePossession(replay.AnchorPossession),
            AnchorPossessionTimeMs = replay.AnchorPossessionTimeMs,
            ReplayStartTimestampNs = segment.StartTimeNs,
            ReplayEndTimestampNs = segment.EndTimeNs,
            Speed = replay.Speed,
            RequiredCompletedLoops = replay.RequiredCompletedLoops,
            Codec = CreateCodec(segment.Codec),
            ParameterSets = [.. segment.ParameterSets.Select(CreateParameterSet)],
            AccessUnits = [.. segment.AccessUnits.Select(CreateAccessUnit)],
        };

        // TODO: This keeps the protocol lean for now.
        // A cleaner flow would let the viewer explicitly stop and restart live playback.
        _LiveVideoStreamController.PauseLiveVideoStream();

        try
        {
            await _LiveAnalysisPublisher.PublishReplayClip(message, ct);
        }
        finally
        {
            _LiveVideoStreamController.ResumeLiveVideoStream();
        }
    }

    private async Task<EncodedReplaySegment?> GetReplaySegment(ReplayPlan replay, CancellationToken ct)
    {
        // Capture may still be encoding the requested tail, especially for a completed pass.
        // A missing start/keyframe cannot be recovered by waiting; an incomplete tail can.
        for (int check = 0; check <= _MaxTailChecks; check++)
        {
            ct.ThrowIfCancellationRequested();
            if (!_ReplayBuffer.TryGetReplaySegment(replay.StartTimestampNs, replay.EndTimestampNs, out EncodedReplaySegment segment))
            {
                return null;
            }

            if (segment.EndTimeNs >= replay.EndTimestampNs)
            {
                return segment;
            }

            if (check < _MaxTailChecks)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(_TailCheckIntervalMs), _TimeProvider, ct);
            }
        }

        return null;
    }

    private static BallPositionMessage CreatePosition(Point point)
        => new()
        {
            X = point.X,
            Y = point.Y,
        };

    private static PossessionMessage CreatePossession(BallPossession possession)
        => new()
        {
            Team = possession.Team switch
            {
                Team.A => TeamMessage.A,
                Team.B => TeamMessage.B,
                _ => TeamMessage.None,
            },
            Area = possession.Area switch
            {
                PossessionArea.Defense => PossessionAreaMessage.Defense,
                PossessionArea.FiveBar => PossessionAreaMessage.FiveBar,
                PossessionArea.ThreeBar => PossessionAreaMessage.ThreeBar,
                _ => PossessionAreaMessage.None,
            },
        };

    private static EncodedReplayCodecMessage CreateCodec(EncodedReplayCodec codec) => codec switch
    {
        EncodedReplayCodec.H264 => EncodedReplayCodecMessage.H264,
        EncodedReplayCodec.H265 => EncodedReplayCodecMessage.H265,
        _ => EncodedReplayCodecMessage.Unknown,
    };

    private static EncodedReplayParameterSetMessage CreateParameterSet(EncodedReplayParameterSet parameterSet)
        => new()
        {
            Type = parameterSet.Type switch
            {
                EncodedReplayParameterSetType.VPS => EncodedReplayParameterSetTypeMessage.VPS,
                EncodedReplayParameterSetType.SPS => EncodedReplayParameterSetTypeMessage.SPS,
                EncodedReplayParameterSetType.PPS => EncodedReplayParameterSetTypeMessage.PPS,
                _ => EncodedReplayParameterSetTypeMessage.Invalid,
            },
            Buffer = parameterSet.Buffer,
        };

    private static EncodedReplayAccessUnitMessage CreateAccessUnit(EncodedReplayAccessUnit accessUnit)
        => new()
        {
            TimeNs = accessUnit.TimeNs,
            IsKeyFrame = accessUnit.IsKeyFrame,
            ContainsAllRequiredParameterSets = accessUnit.ContainsAllRequiredParameterSets,
            Buffer = accessUnit.Buffer,
        };

    private readonly record struct ReplayRequest(
        Guid SessionId,
        Frame TriggerFrame,
        ReplayPlan Replay,
        CancellationToken SessionCancellation);
}
