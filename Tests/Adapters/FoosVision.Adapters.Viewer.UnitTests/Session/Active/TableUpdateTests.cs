// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Active;
using FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;
using FoosVision.Domain.Table.ValueObjects;
using static FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes.TestMessages;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active;

public class TableUpdateTests
{
    [Fact]
    public void Handle_stores_latest_table_configuration()
    {
        TableUpdatePresenter sut = CreateSut(
            out RecordingOverlaySink overlaySink,
            out List<TableConfiguration> trackingConfigurations,
            out List<string> callbacks);

        sut.Handle(CreateTableUpdateMessage());

        Assert.True(sut.LatestTableConfiguration.HasValue);
        Assert.Equal(0xFFFF0000u, overlaySink.TableStates[^1].Bars[0].TeamArgb);
        Assert.Equal(0xFF0000FFu, overlaySink.TableStates[^1].Bars[2].TeamArgb);
        Assert.Equal(8, sut.LatestTableConfiguration.Value.Field.Bars.All.Count());
        Assert.Single(sut.LatestTableConfiguration.Value.Field.Occlusions);
        Assert.Single(overlaySink.TableStates[^1].Occlusions);
        Assert.Single(trackingConfigurations);
        Assert.Equal(["refresh-ui"], callbacks);
    }

    [Fact]
    public void Handle_failed_table_update_clears_latest_table_configuration()
    {
        int resetProjectionCalls = 0;
        TableUpdatePresenter sut = CreateSut(
            out RecordingOverlaySink overlaySink,
            out _,
            out List<string> callbacks,
            () => resetProjectionCalls++);

        sut.Handle(CreateTableUpdateMessage());
        sut.Handle(CreateFailedTableUpdateMessage());

        Assert.False(sut.LatestTableConfiguration.HasValue);
        Assert.Empty(overlaySink.TableStates[^1].Bars);
        Assert.Equal(1, resetProjectionCalls);
        Assert.Equal(["refresh-ui", "refresh-ui"], callbacks);
    }

    private static TableUpdatePresenter CreateSut(
        out RecordingOverlaySink overlaySink,
        out List<TableConfiguration> trackingConfigurations,
        out List<string> callbacks,
        Action? resetTrackingProjection = null)
    {
        List<string> events = [];
        List<TableConfiguration> capturedTrackingConfigurations = [];
        List<string> capturedCallbacks = [];
        overlaySink = new RecordingOverlaySink(events);
        trackingConfigurations = capturedTrackingConfigurations;
        callbacks = capturedCallbacks;

        TableUpdatePresenter sut = new(
            overlaySink,
            capturedTrackingConfigurations.Add,
            resetTrackingProjection ?? (() => { }),
            () => capturedCallbacks.Add("refresh-ui"));
        return sut;
    }
}
