// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Commands;
using FoosVision.Protocol.Messages.Events;
using FoosVision.Protocol.Messages.Live;
using FoosVision.Protocol.Messages.LiveAnalysis;

namespace FoosVision.Protocol;

public static class ProtocolTypeRegistry
{
    public static CommandMessageType GetCommandType<TCommand>()
    {
        var t = typeof(TCommand);

        if (t == typeof(StartInstallCommand)) return CommandMessageType.StartInstall;
        if (t == typeof(StopInstallCommand)) return CommandMessageType.StopInstall;

        if (t == typeof(StartGameCommand)) return CommandMessageType.StartGame;
        if (t == typeof(StopGameCommand)) return CommandMessageType.StopGame;

        throw new NotSupportedException($"Unsupported command type: {t.FullName}");
    }

    public static EventMessageType GetEventType<TEvent>()
    {
        var t = typeof(TEvent);

        if (t == typeof(RecorderRuntimeStateChanged)) return EventMessageType.RecorderRuntimeStateChanged;

        throw new NotSupportedException($"Unsupported event type: {t.FullName}");
    }

    public static Type GetEventClrType(EventMessageType type) => type switch
    {
        EventMessageType.RecorderRuntimeStateChanged => typeof(RecorderRuntimeStateChanged),

        _ => throw new NotSupportedException($"Unsupported event message type: {type}")
    };

    public static LiveMessageType GetLiveMessageType<TMessage>()
    {
        var t = typeof(TMessage);

        if (t == typeof(TrackingFrameMessage)) return LiveMessageType.TrackingFrame;
        if (t == typeof(TableUpdateMessage)) return LiveMessageType.TableUpdate;

        throw new NotSupportedException($"Unsupported live message type: {t.FullName}");
    }

    public static Type GetLiveMessageClrType(LiveMessageType type) => type switch
    {
        LiveMessageType.TrackingFrame => typeof(TrackingFrameMessage),
        LiveMessageType.TableUpdate => typeof(TableUpdateMessage),

        _ => throw new NotSupportedException($"Unsupported live message type: {type}"),
    };

    public static LiveAnalysisMessageType GetLiveAnalysisMessageType<TMessage>()
    {
        var t = typeof(TMessage);

        if (t == typeof(ReplayStartedMessage)) return LiveAnalysisMessageType.ReplayStarted;
        if (t == typeof(ReplayMessage)) return LiveAnalysisMessageType.Replay;
        if (t == typeof(VisionContextMessage)) return LiveAnalysisMessageType.VisionContext;
        if (t == typeof(BallDetectionMaskMessage)) return LiveAnalysisMessageType.BallDetectionMask;

        throw new NotSupportedException($"Unsupported live analysis message type: {t.FullName}");
    }

    public static Type GetLiveAnalysisMessageClrType(LiveAnalysisMessageType type) => type switch
    {
        LiveAnalysisMessageType.ReplayStarted => typeof(ReplayStartedMessage),
        LiveAnalysisMessageType.Replay => typeof(ReplayMessage),
        LiveAnalysisMessageType.VisionContext => typeof(VisionContextMessage),
        LiveAnalysisMessageType.BallDetectionMask => typeof(BallDetectionMaskMessage),

        _ => throw new NotSupportedException($"Unsupported live analysis message type: {type}"),
    };
}
