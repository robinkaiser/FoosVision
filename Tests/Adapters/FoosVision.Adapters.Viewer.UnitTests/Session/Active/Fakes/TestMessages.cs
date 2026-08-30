// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.Events;
using FoosVision.Protocol.Messages.Live;
using FoosVision.Protocol.Messages.LiveAnalysis;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;

internal static class TestMessages
{
    public static RecorderRuntimeStateChanged CreateRuntimeState(RecorderRuntimeMode mode)
    {
        return new RecorderRuntimeStateChanged
        {
            Sequence = 1,
            Mode = mode,
            ActiveSessionId = mode == RecorderRuntimeMode.Idle ? null : Guid.NewGuid(),
            Reason = RecorderStateChangeReason.None,
            Detail = string.Empty,
        };
    }

    public static TrackingFrameMessage CreateTrackingFrame(
        ulong frameId,
        long timestampNs,
        bool isBallFound,
        PointMessage? ballPosition = null)
    {
        return new TrackingFrameMessage
        {
            FrameId = frameId,
            TimestampNs = timestampNs,
            IsBallFound = isBallFound,
            BallPosition = isBallFound ? ballPosition ?? new PointMessage { X = 960, Y = 540 } : null,
            Possession = new PossessionMessage
            {
                Team = TeamMessage.A,
                Area = PossessionAreaMessage.FiveBar,
            },
            PossessionTimeMs = 1234,
            IsTimeFoul = false,
        };
    }

    public static ReplayMessage CreateReplayMessage(
        ulong triggerFrameId = 42,
        long triggerTimestampNs = 2_000_000_000,
        ulong anchorFrameId = 40,
        long anchorTimestampNs = 1_900_000_000,
        long replayEndTimestampNs = 2_900_000_000)
    {
        return new ReplayMessage
        {
            TriggerFrameId = triggerFrameId,
            TriggerTimestampNs = triggerTimestampNs,
            AnchorFrameId = anchorFrameId,
            AnchorTimestampNs = anchorTimestampNs,
            AnchorPosition = new BallPositionMessage { X = 960, Y = 540 },
            AnchorPossession = new PossessionMessage
            {
                Team = TeamMessage.B,
                Area = PossessionAreaMessage.FiveBar,
            },
            AnchorPossessionTimeMs = 9250,
            ReplayStartTimestampNs = anchorTimestampNs,
            ReplayEndTimestampNs = replayEndTimestampNs,
            Codec = EncodedReplayCodecMessage.H264,
            ParameterSets = [],
            AccessUnits =
            [
                CreateAccessUnit(anchorTimestampNs, isKeyFrame: true),
                CreateAccessUnit(anchorTimestampNs + 16_800_000, isKeyFrame: false),
            ],
        };
    }

    public static ReplayStartedMessage CreateReplayStartedMessage(
        ulong triggerFrameId = 42,
        long triggerTimestampNs = 2_000_000_000,
        ulong anchorFrameId = 40,
        long anchorTimestampNs = 1_900_000_000,
        long replayEndTimestampNs = 2_900_000_000)
    {
        return new ReplayStartedMessage
        {
            TriggerFrameId = triggerFrameId,
            TriggerTimestampNs = triggerTimestampNs,
            AnchorFrameId = anchorFrameId,
            AnchorTimestampNs = anchorTimestampNs,
            AnchorPosition = new BallPositionMessage { X = 960, Y = 540 },
            AnchorPossession = new PossessionMessage
            {
                Team = TeamMessage.B,
                Area = PossessionAreaMessage.FiveBar,
            },
            AnchorPossessionTimeMs = 9250,
            ReplayStartTimestampNs = anchorTimestampNs,
            ReplayEndTimestampNs = replayEndTimestampNs,
            Codec = EncodedReplayCodecMessage.H264,
            ParameterSetCount = 2,
            AccessUnitCount = 2,
            AccessUnitBytes = 128,
        };
    }

    public static TableUpdateMessage CreateTableUpdateMessage()
    {
        return new TableUpdateMessage
        {
            TableConfiguration = new TableConfigurationMessage
            {
                Boundary = new TrapeziumMessage
                {
                    UpperLeft = new PointMessage { X = 100, Y = 100 },
                    UpperRight = new PointMessage { X = 1820, Y = 100 },
                    LowerLeft = new PointMessage { X = 120, Y = 980 },
                    LowerRight = new PointMessage { X = 1800, Y = 980 },
                },
                Bars =
                [
                    CreateBar(BarTypeMessage.A1, 200),
                    CreateBar(BarTypeMessage.A2, 400),
                    CreateBar(BarTypeMessage.B3, 600),
                    CreateBar(BarTypeMessage.A5, 800),
                    CreateBar(BarTypeMessage.B5, 1000),
                    CreateBar(BarTypeMessage.A3, 1200),
                    CreateBar(BarTypeMessage.B2, 1400),
                    CreateBar(BarTypeMessage.B1, 1600),
                ],
                Occlusions =
                [
                    new TrapeziumMessage
                    {
                        UpperLeft = new PointMessage { X = 700, Y = 260 },
                        UpperRight = new PointMessage { X = 1220, Y = 260 },
                        LowerLeft = new PointMessage { X = 710, Y = 330 },
                        LowerRight = new PointMessage { X = 1210, Y = 330 },
                    },
                ],
                TeamAPlayerColorArgb = 0xFFFF0000u,
                TeamBPlayerColorArgb = 0xFF0000FFu,
            },
        };
    }

    public static TableUpdateMessage CreateFailedTableUpdateMessage()
    {
        return new TableUpdateMessage
        {
            IsSuccess = false,
            FailureReason = "Detect table configuration failed.",
        };
    }

    private static BarMessage CreateBar(BarTypeMessage type, double x)
    {
        return new BarMessage
        {
            Type = type,
            Left = CreateVerticalLine(x - 10),
            Center = CreateVerticalLine(x),
            Right = CreateVerticalLine(x + 10),
        };
    }

    private static LineMessage CreateVerticalLine(double x)
    {
        return new LineMessage
        {
            P0 = new PointMessage { X = x, Y = 120 },
            P1 = new PointMessage { X = x, Y = 960 },
        };
    }

    private static EncodedReplayAccessUnitMessage CreateAccessUnit(long timeNs, bool isKeyFrame)
    {
        return new EncodedReplayAccessUnitMessage
        {
            TimeNs = timeNs,
            IsKeyFrame = isKeyFrame,
            ContainsAllRequiredParameterSets = true,
            Buffer = [0, 0, 0, 1, isKeyFrame ? (byte)0x65 : (byte)0x41],
        };
    }
}
