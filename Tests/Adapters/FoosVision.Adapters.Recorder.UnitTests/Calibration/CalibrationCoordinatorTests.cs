// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Calibration;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.UseCases.Calibration.UpdateTable;
using FoosVision.UseCases.Calibration.UpdateTableScene;
using NSubstitute;

namespace FoosVision.Adapters.Recorder.UnitTests.Calibration;

public class CalibrationCoordinatorTests
{
    private readonly IFrameFeed _Feed = Substitute.For<IFrameFeed>();
    private readonly IFrameHandle _Handle = Substitute.For<IFrameHandle>();
    private readonly IUpdateTableInputPort _Table = Substitute.For<IUpdateTableInputPort>();
    private readonly IUpdateTableSceneInputPort _Scene = Substitute.For<IUpdateTableSceneInputPort>();
    private readonly IUpdateTableOutputPort _TableOutput = Substitute.For<IUpdateTableOutputPort>();
    private readonly IUpdateTableSceneOutputPort _SceneOutput = Substitute.For<IUpdateTableSceneOutputPort>();
    private readonly TaskCompletionSource _Completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _Active = true;
    private int _Completions;

    private CalibrationCoordinator Create(bool available = true)
    {
        _Handle.Meta.Returns(new Frame(1, 10_000_000_000));
        _Feed.TryAcquireById(1, out Arg.Any<IFrameHandle?>()).Returns(call =>
        {
            call[1] = available ? _Handle : null;
            return available;
        });
        CalibrationSession session = new(() => _Active, _TableOutput, _SceneOutput, Complete, Complete);
        return new(_Table, Substitute.For<ITableConfigFinder>(), _Scene,
            Substitute.For<ITableSceneUpdater>(), _Feed, () => session);
    }

    private Task Complete()
    {
        Interlocked.Increment(ref _Completions);
        _Completed.TrySetResult();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_frame_completes_request_and_does_not_run_vision(bool scene)
    {
        CalibrationCoordinator coordinator = Create(available: false);
        await Request(coordinator, scene);
        Assert.Equal(1, _Completions);
        await _Table.DidNotReceiveWithAnyArgs().Handle(default!, default!, TestContext.Current.CancellationToken);
        await _Scene.DidNotReceiveWithAnyArgs().Handle(default!, default!, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failure_releases_frame_and_completes_request(bool scene)
    {
        CalibrationCoordinator coordinator = Create();
        _Table.Handle(Arg.Any<UpdateTableRequest>(), Arg.Any<IUpdateTableOutputPort>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("test")));
        _Scene.Handle(Arg.Any<UpdateTableSceneRequest>(), Arg.Any<IUpdateTableSceneOutputPort>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("test")));
        await Request(coordinator, scene);
        await _Completed.Task;
        _Handle.Received(1).Release();
        Assert.Equal(1, _Completions);
        if (scene) await _SceneOutput.Received(1).ReportFailure(Arg.Any<string>());
        else await _TableOutput.Received(1).ReportFailure(Arg.Any<string>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stop_or_session_replacement_discards_late_output_and_still_completes(bool scene)
    {
        CalibrationCoordinator coordinator = Create();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _Table.Handle(Arg.Any<UpdateTableRequest>(), Arg.Any<IUpdateTableOutputPort>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                entered.SetResult();
                await resume.Task;
                await call.Arg<IUpdateTableOutputPort>().ReportFailure("late");
            });
        _Scene.Handle(Arg.Any<UpdateTableSceneRequest>(), Arg.Any<IUpdateTableSceneOutputPort>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                entered.SetResult();
                await resume.Task;
                await call.Arg<IUpdateTableSceneOutputPort>().ReportSuccess();
            });
        await Request(coordinator, scene);
        await entered.Task;
        _Active = false;
        resume.SetResult();
        await _Completed.Task;
        await _TableOutput.DidNotReceiveWithAnyArgs().ReportFailure(default!);
        await _SceneOutput.DidNotReceive().ReportSuccess();
        _Handle.Received(1).Release();
        Assert.Equal(1, _Completions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Successful_update_forwards_output_releases_frame_and_completes(bool scene)
    {
        CalibrationCoordinator coordinator = Create();
        TableConfiguration config = new(null!, default, default);
        _Table.Handle(Arg.Any<UpdateTableRequest>(), Arg.Any<IUpdateTableOutputPort>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IUpdateTableOutputPort>().ReportSuccess(config));
        _Scene.Handle(Arg.Any<UpdateTableSceneRequest>(), Arg.Any<IUpdateTableSceneOutputPort>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IUpdateTableSceneOutputPort>().ReportSuccess());
        await Request(coordinator, scene);
        await _Completed.Task;
        _Handle.Received(1).Release();
        Assert.Equal(1, _Completions);
        if (scene) await _SceneOutput.Received(1).ReportSuccess();
        else await _TableOutput.Received(1).ReportSuccess(config);
    }

    private static Task Request(CalibrationCoordinator coordinator, bool scene) => scene
        ? coordinator.RequestTableSceneUpdate(new Frame(1, 0), Option<Point>.None())
        : coordinator.RequestTableUpdate(new Frame(1, 0));
}
