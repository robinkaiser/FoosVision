// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Threading.Channels;
using FoosVision.Common.Logging;
using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Ports.Media;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Live;
using FoosVision.Protocol.Messages.LiveAnalysis;

namespace FoosVision.Adapters.Recorder.Game.Orchestration;

public class ReplayCoordinator : IReplayCoordinator, IDisposable
{
    private const long _BallDisappearedReplayDurationNs = 1_000L * 1_000_000L;
    private const long _SavedShotReplayDurationNs = 700L * 1_000_000L;

    private static readonly Source _Log = new("ReplayCoordinator");

    private readonly IEncodedReplayBuffer _ReplayBuffer;
    private readonly ILiveVideoStreamController _LiveVideoStreamController;
    private readonly IRecorderLiveAnalysisPublisher _LiveAnalysisPublisher;
    private readonly Channel<ReplayRequest> _Requests;
    private readonly CancellationTokenSource _Cts = new();
    private readonly Task _Worker;
    private int _ReplaySendPendingOrRunning;

    public ReplayCoordinator(
        IEncodedReplayBuffer replayBuffer,
        ILiveVideoStreamController liveVideoStreamController,
        IRecorderLiveAnalysisPublisher liveAnalysisPublisher)
    {
        _ReplayBuffer = replayBuffer;
        _LiveVideoStreamController = liveVideoStreamController;
        _LiveAnalysisPublisher = liveAnalysisPublisher;
        _Requests = Channel.CreateBounded<ReplayRequest>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite,
        });
        _Worker = Task.Run(() => ProcessRequests(_Cts.Token));
    }

    public Task RequestReplay(
        Frame triggerFrame,
        Frame anchorFrame,
        Point anchorPosition,
        BallPossession anchorPossession,
        int anchorPossessionTimeMs,
        ReplayTriggerKind triggerKind)
    {
        if (!TryBeginReplaySend())
        {
            _Log.Warning("Request skipped, another request is being prepared or sent. TriggerFrameId={FrameId}", triggerFrame.Id);
            return Task.CompletedTask;
        }

        ReplayRequest request = new(
            triggerFrame,
            anchorFrame,
            anchorPosition,
            anchorPossession,
            anchorPossessionTimeMs,
            triggerKind);

        if (!_Requests.Writer.TryWrite(request))
        {
            EndReplaySend();
            _Log.Warning("Request skipped, the worker send queue is full. TriggerFrameId={FrameId}", triggerFrame.Id);
            return Task.CompletedTask;
        }

        _Log.Information(
            "Request accepted. TriggerFrameId={TriggerFrameId} AnchorFrameId={AnchorFrameId} AnchorTimestampNs={AnchorTimestampNs} AnchorPosition=({X},{Y}) ReplayStartNs={ReplayStartNs} ReplayEndNs={ReplayEndNs}",
            triggerFrame.Id,
            anchorFrame.Id,
            anchorFrame.TimestampNs,
            anchorPosition.X,
            anchorPosition.Y,
            anchorFrame.TimestampNs,
            anchorFrame.TimestampNs + GetReplayDurationNs(triggerKind));
        return Task.CompletedTask;
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
                    await ProcessRequest(request, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _Log.Error("Request failed. TriggerFrameId={FrameId} Ex={Exception}", request.TriggerFrame.Id, ex.ToString());
                }
                finally
                {
                    EndReplaySend();
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task ProcessRequest(ReplayRequest request, CancellationToken ct)
    {
        long startTimeNs = request.AnchorFrame.TimestampNs;
        long endTimeNs = startTimeNs + GetReplayDurationNs(request.TriggerKind);

        if (!_ReplayBuffer.TryGetReplaySegment(startTimeNs, endTimeNs, out EncodedReplaySegment segment))
        {
            _Log.Warning(
                "Replay segment unavailable. TriggerFrameId={TriggerFrameId} AnchorFrameId={AnchorFrameId}",
                request.TriggerFrame.Id,
                request.AnchorFrame.Id);
            return;
        }

        int accessUnitBytes = segment.AccessUnits.Sum(accessUnit => accessUnit.Buffer.Length);

        ReplayStartedMessage startedMessage = new()
        {
            TriggerFrameId = request.TriggerFrame.Id,
            TriggerTimestampNs = request.TriggerFrame.TimestampNs,
            AnchorFrameId = request.AnchorFrame.Id,
            AnchorTimestampNs = request.AnchorFrame.TimestampNs,
            AnchorPosition = CreatePosition(request.AnchorPosition),
            AnchorPossession = CreatePossession(request.AnchorPossession),
            AnchorPossessionTimeMs = request.AnchorPossessionTimeMs,
            ReplayStartTimestampNs = segment.StartTimeNs,
            ReplayEndTimestampNs = segment.EndTimeNs,
            Codec = CreateCodec(segment.Codec),
            ParameterSetCount = segment.ParameterSets.Count,
            AccessUnitCount = segment.AccessUnits.Count,
            AccessUnitBytes = accessUnitBytes,
        };

        await _LiveAnalysisPublisher.PublishReplayStarted(startedMessage, ct);

        ReplayMessage message = new()
        {
            TriggerFrameId = request.TriggerFrame.Id,
            TriggerTimestampNs = request.TriggerFrame.TimestampNs,
            AnchorFrameId = request.AnchorFrame.Id,
            AnchorTimestampNs = request.AnchorFrame.TimestampNs,
            AnchorPosition = CreatePosition(request.AnchorPosition),
            AnchorPossession = CreatePossession(request.AnchorPossession),
            AnchorPossessionTimeMs = request.AnchorPossessionTimeMs,
            ReplayStartTimestampNs = segment.StartTimeNs,
            ReplayEndTimestampNs = segment.EndTimeNs,
            Codec = CreateCodec(segment.Codec),
            ParameterSets = [.. segment.ParameterSets.Select(CreateParameterSet)],
            AccessUnits = [.. segment.AccessUnits.Select(CreateAccessUnit)],
        };

        // TODO: This keeps the protocol lean for now.
        // A cleaner flow would let the viewer explicitly stop and restart live playback.
        _LiveVideoStreamController.PauseLiveVideoStream();

        try
        {
            await _LiveAnalysisPublisher.PublishReplay(message, ct);
        }
        finally
        {
            _LiveVideoStreamController.ResumeLiveVideoStream();
        }
    }

    private bool TryBeginReplaySend()
        => Interlocked.CompareExchange(ref _ReplaySendPendingOrRunning, 1, 0) == 0;

    private void EndReplaySend()
        => Interlocked.Exchange(ref _ReplaySendPendingOrRunning, 0);

    private static long GetReplayDurationNs(ReplayTriggerKind triggerKind)
    {
        return triggerKind switch
        {
            ReplayTriggerKind.SavedShot => _SavedShotReplayDurationNs,
            _ => _BallDisappearedReplayDurationNs,
        };
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
        Frame TriggerFrame,
        Frame AnchorFrame,
        Point AnchorPosition,
        BallPossession AnchorPossession,
        int AnchorPossessionTimeMs,
        ReplayTriggerKind TriggerKind);
}
