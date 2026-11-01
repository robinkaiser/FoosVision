// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.Adapters.Recorder.Game.Control;
using FoosVision.Adapters.Recorder.Setup.Control;
using FoosVision.Adapters.Recorder.Training.Control;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Commands;
using FoosVision.Protocol.Messages.Common;
using FoosVision.UseCases.Game.StartGame;
using FoosVision.UseCases.Game.StopGame;
using FoosVision.UseCases.Setup.StartSetup;
using FoosVision.UseCases.Setup.StopSetup;
using FoosVision.UseCases.Training.StartTraining;
using FoosVision.UseCases.Training.StopTraining;
using NSubstitute;

namespace FoosVision.Adapters.Recorder.UnitTests.Connectivity;

public class RecorderCommandRouterTests
{
    private readonly IStartSetupInputPort _StartSetup = Substitute.For<IStartSetupInputPort>();
    private readonly IStartTrainingInputPort _StartTraining = Substitute.For<IStartTrainingInputPort>();
    private readonly IStopTrainingInputPort _StopTraining = Substitute.For<IStopTrainingInputPort>();
    private readonly IStartTrainingOutputPort _TrainingOutput = Substitute.For<IStartTrainingOutputPort>();

    private RecorderCommandRouter Create() => new(
        new SetupCommandHandler(_StartSetup, Substitute.For<IStopSetupInputPort>(),
            _ => Substitute.For<IStartSetupOutputPort>(), _ => Substitute.For<IStopSetupOutputPort>()),
        new GameCommandHandler(Substitute.For<IStartGameInputPort>(), Substitute.For<IStopGameInputPort>(),
            _ => Substitute.For<IStartGameOutputPort>(), _ => Substitute.For<IStopGameOutputPort>()),
        new TrainingCommandHandler(_StartTraining, _StopTraining,
            _ => _TrainingOutput, _ => Substitute.For<IStopTrainingOutputPort>()));

    [Fact]
    public async Task Training_commands_are_dispatched_and_invalid_payloads_are_rejected()
    {
        RecorderCommandRouter router = Create();
        Guid id = Guid.NewGuid();
        CommandResponse response = await router.DispatchAsync(CommandMessageType.StartTraining, new StartTrainingCommand { CommandId = id }, TestContext.Current.CancellationToken);
        Assert.True(response.Accepted);
        Assert.Equal(id, response.CommandId);
        await router.DispatchAsync(CommandMessageType.StopTraining, new StopTrainingCommand(), TestContext.Current.CancellationToken);
        await router.RunExclusive(() => Task.CompletedTask);
        await _StartTraining.Received(1).Handle(Arg.Any<StartTrainingRequest>(), _TrainingOutput, TestContext.Current.CancellationToken);
        await _StopTraining.Received(1).Handle(Arg.Any<StopTrainingRequest>(), Arg.Any<IStopTrainingOutputPort>(), TestContext.Current.CancellationToken);
        Assert.False((await router.DispatchAsync(CommandMessageType.StartTraining, new StartGameCommand(), TestContext.Current.CancellationToken)).Accepted);
    }

    [Fact]
    public async Task Session_stop_waits_for_inflight_command_and_allows_following_start_without_reconnect()
    {
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _StartSetup.Handle(Arg.Any<StartSetupRequest>(), Arg.Any<IStartSetupOutputPort>(), Arg.Any<CancellationToken>()).Returns(start.Task);
        RecorderCommandRouter router = Create();
        await router.DispatchAsync(CommandMessageType.StartSetup, new StartSetupCommand(), TestContext.Current.CancellationToken);
        bool stopped = false;
        Task stop = router.RunExclusive(() => { stopped = true; return Task.CompletedTask; });
        CommandResponse response = await router.DispatchAsync(CommandMessageType.StartTraining, new StartTrainingCommand(), TestContext.Current.CancellationToken);
        Assert.True(response.Accepted);
        Assert.False(stopped);
        await _StartTraining.DidNotReceiveWithAnyArgs().Handle(default!, default!, TestContext.Current.CancellationToken);

        start.SetResult();
        await stop;
        await router.RunExclusive(() => Task.CompletedTask);

        Assert.True(stopped);
        await _StartTraining.Received(1).Handle(Arg.Any<StartTrainingRequest>(), Arg.Any<IStartTrainingOutputPort>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Canceled_queued_command_is_not_executed()
    {
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _StartSetup.Handle(Arg.Any<StartSetupRequest>(), Arg.Any<IStartSetupOutputPort>(), Arg.Any<CancellationToken>()).Returns(start.Task);
        RecorderCommandRouter router = Create();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await router.DispatchAsync(CommandMessageType.StartSetup, new StartSetupCommand(), TestContext.Current.CancellationToken);
        await router.DispatchAsync(CommandMessageType.StartTraining, new StartTrainingCommand(), cancellation.Token);

        cancellation.Cancel();
        start.SetResult();
        await router.RunExclusive(() => Task.CompletedTask);

        await _StartTraining.DidNotReceiveWithAnyArgs().Handle(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Training_start_exception_does_not_block_following_commands()
    {
        _StartTraining.Handle(Arg.Any<StartTrainingRequest>(), Arg.Any<IStartTrainingOutputPort>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("startup failed"));
        RecorderCommandRouter router = Create();
        await router.DispatchAsync(CommandMessageType.StartTraining, new StartTrainingCommand(), TestContext.Current.CancellationToken);
        await router.DispatchAsync(CommandMessageType.StopTraining, new StopTrainingCommand(), TestContext.Current.CancellationToken);
        await router.RunExclusive(() => Task.CompletedTask);
        await _StopTraining.Received(1).Handle(Arg.Any<StopTrainingRequest>(), Arg.Any<IStopTrainingOutputPort>(), TestContext.Current.CancellationToken);
    }
}
