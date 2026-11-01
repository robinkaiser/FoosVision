// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Commands;
using FoosVision.Protocol.Messages.Events;
using FoosVision.Protocol.Messages.LiveAnalysis;

namespace FoosVision.Adapters.Viewer.UnitTests;

public class TrainingProtocolTests
{
    [Fact]
    public void Live_analysis_message_ids_and_type_registry_match()
    {
        AssertLiveAnalysis<VisionContextMessage>(LiveAnalysisMessageType.VisionContext, 1);
        AssertLiveAnalysis<BallDetectionMaskMessage>(LiveAnalysisMessageType.BallDetectionMask, 2);
        AssertLiveAnalysis<ReplayStartedMessage>(LiveAnalysisMessageType.ReplayStarted, 3);
        AssertLiveAnalysis<ReplayClipMessage>(LiveAnalysisMessageType.ReplayClip, 4);
        AssertLiveAnalysis<ReplayReturnToLiveMessage>(LiveAnalysisMessageType.ReplayReturnToLive, 5);
    }

    [Fact]
    public void Command_ids_and_type_registry_match()
    {
        AssertCommand<StartSetupCommand>(CommandMessageType.StartSetup, 1);
        AssertCommand<StopSetupCommand>(CommandMessageType.StopSetup, 2);
        AssertCommand<StartGameCommand>(CommandMessageType.StartGame, 10);
        AssertCommand<StopGameCommand>(CommandMessageType.StopGame, 11);
        AssertCommand<StartTrainingCommand>(CommandMessageType.StartTraining, 20);
        AssertCommand<StopTrainingCommand>(CommandMessageType.StopTraining, 21);
    }

    [Fact]
    public void Runtime_modes_use_explicit_values()
    {
        Assert.Equal(0, (int)RecorderRuntimeMode.Idle);
        Assert.Equal(1, (int)RecorderRuntimeMode.SetupRunning);
        Assert.Equal(2, (int)RecorderRuntimeMode.GameRunning);
        Assert.Equal(3, (int)RecorderRuntimeMode.TrainingRunning);
        Assert.Equal(4, (int)RecorderRuntimeMode.Faulted);
    }

    private static void AssertLiveAnalysis<TMessage>(LiveAnalysisMessageType expectedType, byte expectedValue)
    {
        Assert.Equal(expectedValue, (byte)expectedType);
        Assert.Equal(expectedType, ProtocolTypeRegistry.GetLiveAnalysisMessageType<TMessage>());
        Assert.Equal(typeof(TMessage), ProtocolTypeRegistry.GetLiveAnalysisMessageClrType(expectedType));
    }

    private static void AssertCommand<TCommand>(CommandMessageType expectedType, byte expectedValue)
        where TCommand : ICommand
    {
        CommandMessageType type = ProtocolTypeRegistry.GetCommandType<TCommand>();
        Assert.Equal(expectedType, type);
        Assert.Equal(expectedValue, (byte)type);
        Assert.Equal(typeof(TCommand), ProtocolTypeRegistry.GetCommandClrType(type));
    }
}
