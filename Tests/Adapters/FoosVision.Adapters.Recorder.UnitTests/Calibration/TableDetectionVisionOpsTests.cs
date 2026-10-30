// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Calibration;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.UseCases.Calibration.Ports;
using FoosVision.UseCases.Calibration.UpdateTable;
using NSubstitute;

namespace FoosVision.Adapters.Recorder.UnitTests.Calibration;

public class TableDetectionVisionOpsTests
{
    [Fact]
    public async Task Session_change_during_detection_prevents_shared_configuration_write()
    {
        bool active = true;
        ITableConfigFinder finder = Substitute.For<ITableConfigFinder>();
        ITableConfigStore store = Substitute.For<ITableConfigStore>();
        IUpdateTableOutputPort output = Substitute.For<IUpdateTableOutputPort>();
        TableConfiguration config = new(null!, default, default);
        store.LoadTableConfig().Returns(Option<TableConfiguration>.Some(config));
        finder.Detect(Arg.Any<byte[]>()).Returns(_ =>
        {
            active = false;
            return Option<TableConfiguration>.Some(config);
        });
        TableDetectionVisionOps vision = new(finder, Substitute.For<IFrameHandle>(), () => active);
        UpdateTableInteractor interactor = new(store);

        await Assert.ThrowsAsync<OperationCanceledException>(() => interactor.Handle(
            new(new Frame(1, 0), vision, UpdateMode.Update), output, TestContext.Current.CancellationToken));

        store.DidNotReceiveWithAnyArgs().SaveTableConfig(default!);
        await output.DidNotReceiveWithAnyArgs().ReportSuccess(default!);
    }
}
