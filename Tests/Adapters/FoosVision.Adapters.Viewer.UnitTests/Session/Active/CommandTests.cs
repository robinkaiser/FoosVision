// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session;
using FoosVision.Adapters.Viewer.Session.Active;
using FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;
using NSubstitute;
using static FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes.TestMessages;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active;

public class CommandTests
{
    [Fact]
    public async Task ToggleModeSessionAsync_start_install_refreshes_playback_before_sending_command()
    {
        SessionContext context = new();
        using ActiveSession sut = context.CreateSut();

        await sut.ToggleModeSessionAsync(SessionMode.Install);

        Assert.Equal(["clear-tracking", "stop-playback", "start-playback"], context.Events);
        Assert.True(context.UiSink.States[^1].IsPendingCommand);
        await context.Session.Received(1).StartInstallAsync(Arg.Any<Guid>(), CancellationToken.None);
    }

    [Fact]
    public async Task ToggleModeSessionAsync_start_game_without_table_configuration_does_not_send_command()
    {
        SessionContext context = new();
        using ActiveSession sut = context.CreateSut();

        await sut.ToggleModeSessionAsync(SessionMode.Game);

        Assert.Equal(SessionMode.Install, context.UiSink.States[^1].Mode);
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
}
