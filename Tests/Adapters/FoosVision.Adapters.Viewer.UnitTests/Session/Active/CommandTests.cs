// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session;
using FoosVision.Adapters.Viewer.Session.Active;
using FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;
using FoosVision.Protocol.Messages.Common;
using FoosVision.Protocol.Messages.Events;
using NSubstitute;
using static FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes.TestMessages;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active;

public class CommandTests
{
    [Fact]
    public async Task ToggleModeSessionAsync_start_setup_refreshes_playback_before_sending_command()
    {
        SessionContext context = new();
        using ActiveSession sut = context.CreateSut();

        await sut.ToggleModeSessionAsync(SessionMode.Setup);

        Assert.Equal(["clear-tracking", "stop-playback", "start-playback"], context.Events);
        Assert.True(context.UiSink.States[^1].IsPendingCommand);
        await context.Session.Received(1).StartSetupAsync(Arg.Any<Guid>(), CancellationToken.None);
    }

    [Fact]
    public async Task ToggleModeSessionAsync_start_game_without_table_configuration_does_not_send_command()
    {
        SessionContext context = new();
        using ActiveSession sut = context.CreateSut();

        await sut.ToggleModeSessionAsync(SessionMode.Game);

        Assert.Equal(SessionMode.Setup, context.UiSink.States[^1].Mode);
        Assert.False(context.UiSink.States[^1].IsPendingCommand);
        await context.Session.DidNotReceive().StartGameAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleModeSessionAsync_start_game_after_table_configuration_sets_pending_state_and_uses_game_command()
    {
        SessionContext context = new();
        using ActiveSession sut = context.CreateSut();

        context.PublishTableUpdate(CreateTableUpdateMessage());
        await sut.ToggleModeSessionAsync(SessionMode.Game);

        Assert.Equal(SessionMode.Game, context.UiSink.States[^1].Mode);
        Assert.True(context.UiSink.States[^1].IsPendingCommand);
        Assert.True(context.UiSink.States[^1].IsGameAvailable);
        await context.Session.Received(1).StartGameAsync(Arg.Any<Guid>(), CancellationToken.None);
    }

    [Fact]
    public async Task ToggleModeSessionAsync_start_training_without_table_configuration_does_not_send_command()
    {
        SessionContext context = new();
        using ActiveSession sut = context.CreateSut();

        await sut.ToggleModeSessionAsync(SessionMode.Training);

        Assert.Equal(SessionMode.Setup, context.UiSink.States[^1].Mode);
        Assert.False(context.UiSink.States[^1].IsPendingCommand);
        await context.Session.DidNotReceive().StartTrainingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleModeSessionAsync_start_training_after_table_configuration_sets_pending_state_and_uses_training_command()
    {
        SessionContext context = new();
        using ActiveSession sut = context.CreateSut();

        context.PublishTableUpdate(CreateTableUpdateMessage());
        await sut.ToggleModeSessionAsync(SessionMode.Training);

        Assert.Equal(SessionMode.Training, context.UiSink.States[^1].Mode);
        Assert.True(context.UiSink.States[^1].IsPendingCommand);
        Assert.True(context.UiSink.States[^1].IsTrainingAvailable);
        Assert.Equal(["clear-tracking", "stop-playback", "start-playback"], context.Events);
        await context.Session.Received(1).StartTrainingAsync(Arg.Any<Guid>(), CancellationToken.None);
    }

    [Fact]
    public async Task ToggleModeSessionAsync_stops_training_and_blocks_other_modes()
    {
        SessionContext context = new();
        using ActiveSession sut = context.CreateSut();
        context.PublishTableUpdate(CreateTableUpdateMessage());
        sut.OnRecorderRuntimeStateChanged(CreateRuntimeState(RecorderRuntimeMode.TrainingRunning));

        await sut.ToggleModeSessionAsync(SessionMode.Setup);
        await sut.ToggleModeSessionAsync(SessionMode.Game);
        await sut.ToggleModeSessionAsync(SessionMode.Training);
        await sut.ToggleModeSessionAsync(SessionMode.Training);

        await context.Session.DidNotReceive().StartSetupAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await context.Session.DidNotReceive().StartGameAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await context.Session.Received(1).StopTrainingAsync(Arg.Any<Guid>(), CancellationToken.None);
        Assert.True(context.UiSink.States[^1].IsPendingCommand);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToggleModeSessionAsync_training_failure_clears_pending_state(bool throws)
    {
        SessionContext context = new();
        using ActiveSession sut = context.CreateSut();
        context.PublishTableUpdate(CreateTableUpdateMessage());
        context.Session.StartTrainingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => throws
                ? Task.FromException<CommandResponse>(new InvalidOperationException("Send failed"))
                : Task.FromResult(new CommandResponse { CommandId = call.ArgAt<Guid>(0), Accepted = false, Error = "Rejected" }));

        await sut.ToggleModeSessionAsync(SessionMode.Training);

        Assert.False(context.UiSink.States[^1].IsPendingCommand);
        Assert.False(context.UiSink.States[^1].IsRunning);
    }
}
