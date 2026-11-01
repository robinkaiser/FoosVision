// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.Training.Services;
using FoosVision.Domain.Training.ValueObjects;
using FoosVision.Domain.UnitTests;
using FoosVision.UseCases.Common;
using FoosVision.UseCases.Dependencies.Settings;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Game.StartGame;
using FoosVision.UseCases.Setup.Ports;
using FoosVision.UseCases.Setup.StartSetup;
using FoosVision.UseCases.Training.Ports;
using FoosVision.UseCases.Training.StartTraining;
using FoosVision.UseCases.UnitTests.Game;
using FoosVision.UseCases.UnitTests.Training;
using NSubstitute;

namespace FoosVision.UseCases.UnitTests.Common;

public class SessionGateTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    public async Task Concurrent_start_is_rejected_before_configuring_the_shared_source(int firstMode, int secondMode)
    {
        SessionGate sessionGate = new();
        IFrameSource frames = Substitute.For<IFrameSource>();
        TaskCompletionSource<FrameSourceResult> configured = new(TaskCreationOptions.RunContinuationsAsynchronously);
        frames.Configure(Arg.Any<CancellationToken>()).Returns(configured.Task);
        frames.Start(Arg.Any<CancellationToken>()).Returns(FrameSourceResult.Success);
        ISettingsStore settings = Substitute.For<ISettingsStore>();
        settings.LoadTableConfig().Returns(Option<TableConfiguration>.Some(TableConfig.Config));
        FakeGameSessionStore games = new();
        games.Clear();
        ITrainingOptionsProvider options = Substitute.For<ITrainingOptionsProvider>();
        options.LoadTrainingOptions().Returns(TrainingOptions.Default);
        IStartSetupOutputPort setupOutput = Substitute.For<IStartSetupOutputPort>();
        IStartGameOutputPort gameOutput = Substitute.For<IStartGameOutputPort>();
        IStartTrainingOutputPort trainingOutput = Substitute.For<IStartTrainingOutputPort>();
        Guid startedSessionId = Guid.Empty;
        setupOutput.ReportStarted(Arg.Any<StartSetupResponse>()).Returns(call =>
        {
            startedSessionId = call.Arg<StartSetupResponse>().SessionId;
            return Task.CompletedTask;
        });
        gameOutput.ReportStarted(Arg.Any<StartGameResponse>()).Returns(call =>
        {
            startedSessionId = call.Arg<StartGameResponse>().SessionId;
            return Task.CompletedTask;
        });
        trainingOutput.ReportStarted(Arg.Any<StartTrainingResponse>()).Returns(call =>
        {
            startedSessionId = call.Arg<StartTrainingResponse>().SessionId;
            return Task.CompletedTask;
        });
        StartSetupInteractor setup = new(Substitute.For<ISetupSessionStore>(), frames, sessionGate);
        StartGameInteractor game = new(games, settings, frames, sessionGate);
        StartTrainingInteractor training = new(new FakeTrainingSessionStore(), settings, frames, options,
            Substitute.For<ITrainingRandom>(), sessionGate);
        Task Start(int mode) => mode switch
        {
            0 => setup.Handle(new(), setupOutput, TestContext.Current.CancellationToken),
            1 => game.Handle(new(), gameOutput, TestContext.Current.CancellationToken),
            _ => training.Handle(new(), trainingOutput, TestContext.Current.CancellationToken),
        };

        Task first = Start(firstMode);
        Assert.False(first.IsCompleted);
        await Start(secondMode);
        await frames.Received(1).Configure(Arg.Any<CancellationToken>());
        await frames.DidNotReceive().Start(Arg.Any<CancellationToken>());
        switch (secondMode)
        {
            case 0: await setupOutput.Received(1).ReportStartFailed(Arg.Any<string>()); break;
            case 1: await gameOutput.Received(1).ReportStartFailed(Arg.Any<string>()); break;
            case 2: await trainingOutput.Received(1).ReportStartFailed(Arg.Any<string>()); break;
        }
        configured.SetResult(FrameSourceResult.Success);
        await first;
        await frames.Received(1).Start(Arg.Any<CancellationToken>());
        Assert.Null(sessionGate.TryReserve());
        await Start(secondMode);
        await frames.Received(1).Configure(Arg.Any<CancellationToken>());
        Assert.NotEqual(Guid.Empty, startedSessionId);
        sessionGate.Release(startedSessionId);
        using SessionGate.Reservation? next = sessionGate.TryReserve();
        Assert.NotNull(next);
    }

    [Fact]
    public void Failed_reservations_release_and_stale_release_does_not_end_a_new_session()
    {
        SessionGate sessionGate = new();
        using (SessionGate.Reservation? failed = sessionGate.TryReserve()) Assert.NotNull(failed);
        Guid first;
        using (SessionGate.Reservation? reservation = sessionGate.TryReserve())
        {
            Assert.NotNull(reservation);
            first = reservation.SessionId;
            Assert.NotEqual(Guid.Empty, first);
            reservation.Commit();
            Assert.Equal(first, reservation.SessionId);
        }
        sessionGate.Release(first);
        using SessionGate.Reservation? next = sessionGate.TryReserve();
        Assert.NotNull(next);
        Assert.NotEqual(first, next.SessionId);
        next.Commit();
        sessionGate.Release(first);
        Assert.Null(sessionGate.TryReserve());
    }
}
