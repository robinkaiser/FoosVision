// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Installation.Live;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Live;
using FoosVision.UseCases.Game.Ports;
using NSubstitute;

namespace FoosVision.Adapters.Recorder.UnitTests.Installation.Live;

public class TableUpdatePresenterTests
{
    [Fact]
    public async Task Install_report_failure_publishes_failed_table_update()
    {
        RecordingLiveDataPublisher liveDataPublisher = new();
        TableUpdatePresenter testee = new(liveDataPublisher);

        await testee.ReportFailure("Detect table configuration failed.");

        Assert.NotNull(liveDataPublisher.TableUpdate);
        Assert.False(liveDataPublisher.TableUpdate.IsSuccess);
        Assert.Equal("Detect table configuration failed.", liveDataPublisher.TableUpdate.FailureReason);
    }

    [Fact]
    public async Task Game_report_failure_does_not_publish_failed_table_update()
    {
        RecordingLiveDataPublisher liveDataPublisher = new();
        Recorder.Game.Live.TableUpdatePresenter testee = new(
            Substitute.For<IGameSessionStore>(),
            liveDataPublisher);

        await testee.ReportFailure("Detect table configuration failed.");

        Assert.Null(liveDataPublisher.TableUpdate);
    }

    private class RecordingLiveDataPublisher : IRecorderLiveDataPublisher
    {
        public TableUpdateMessage? TableUpdate { get; private set; }

        public Task PublishTrackingFrame(TrackingFrameMessage frame, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task PublishTableUpdate(TableUpdateMessage update, CancellationToken ct = default)
        {
            TableUpdate = update;
            return Task.CompletedTask;
        }
    }
}
