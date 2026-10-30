// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.UseCases.Calibration.UpdateTable;
using FoosVision.UseCases.Calibration.UpdateTableScene;

namespace FoosVision.Adapters.Recorder.Calibration;

public record CalibrationSession(
    Func<bool> IsActive,
    IUpdateTableOutputPort TableOutput,
    IUpdateTableSceneOutputPort SceneOutput,
    Func<Task> CompleteTable,
    Func<Task> CompleteScene);

public class CalibrationCoordinator : ICalibrationCoordinator
{
    private static readonly Source _Log = new("CalibrationCoordinator");
    private readonly IUpdateTableInputPort _UpdateTable;
    private readonly ITableConfigFinder _TableFinder;
    private readonly IUpdateTableSceneInputPort _UpdateScene;
    private readonly ITableSceneUpdater _SceneUpdater;
    private readonly IFrameFeed _FrameFeed;
    private readonly Func<CalibrationSession?> _CaptureSession;
    private int _UpdateInProgress;

    public CalibrationCoordinator(
        IUpdateTableInputPort updateTable,
        ITableConfigFinder tableFinder,
        IUpdateTableSceneInputPort updateScene,
        ITableSceneUpdater sceneUpdater,
        IFrameFeed frameFeed,
        Func<CalibrationSession?> captureSession)
    {
        _UpdateTable = updateTable;
        _TableFinder = tableFinder;
        _UpdateScene = updateScene;
        _SceneUpdater = sceneUpdater;
        _FrameFeed = frameFeed;
        _CaptureSession = captureSession;
    }

    public Task RequestTableUpdate(Frame frame) => Request(frame, true, Option<Point>.None());

    public Task RequestTableSceneUpdate(Frame frame, Option<Point> ballPosition) => Request(frame, false, ballPosition);

    private Task Request(Frame frame, bool table, Option<Point> ballPosition)
    {
        CalibrationSession? session = _CaptureSession();
        if (session == null) return Task.CompletedTask;

        Func<Task> complete = table ? session.CompleteTable : session.CompleteScene;
        if (Interlocked.CompareExchange(ref _UpdateInProgress, 1, 0) != 0)
        {
            return complete();
        }

        IFrameHandle? handle;
        try
        {
            if (!_FrameFeed.TryAcquireById(frame.Id, out handle))
            {
                _Log.Warning("Calibration frame not in pool. FrameId={FrameId}", frame.Id);
                Interlocked.Exchange(ref _UpdateInProgress, 0);
                return complete();
            }
        }
        catch (Exception ex)
        {
            _Log.Error("Acquiring calibration frame failed. FrameId={FrameId} Ex={Exception}", frame.Id, ex.ToString());
            Interlocked.Exchange(ref _UpdateInProgress, 0);
            return complete();
        }

        _ = Execute(handle, session, table, ballPosition, complete);
        return Task.CompletedTask;
    }

    private Task Execute(IFrameHandle handle, CalibrationSession session, bool table, Option<Point> ballPosition, Func<Task> complete)
    {
        return Task.Run(async () =>
        {
            try
            {
                if (!session.IsActive()) return;
                ScopedOutput output = new(session);
                if (table)
                {
                    TableDetectionVisionOps vision = new(_TableFinder, handle, session.IsActive);
                    await _UpdateTable.Handle(new(handle.Meta, vision, UpdateMode.Update), output, CancellationToken.None);
                }
                else
                {
                    TableSceneUpdateVisionOps vision = new(_SceneUpdater, handle, session.IsActive);
                    await _UpdateScene.Handle(new(handle.Meta, ballPosition, vision), output, CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (!session.IsActive())
            {
            }
            catch (Exception ex)
            {
                _Log.Error("Calibration failed. FrameId={FrameId} Ex={Exception}", handle.Meta.Id, ex.ToString());
                if (session.IsActive())
                {
                    try
                    {
                        if (table) await session.TableOutput.ReportFailure($"Exception: {ex.Message}");
                        else await session.SceneOutput.ReportFailure($"Unhandled exception: {ex.Message}");
                    }
                    catch (Exception reportError)
                    {
                        _Log.Error("Calibration failure reporting failed. Ex={Exception}", reportError.ToString());
                    }
                }
            }
            finally
            {
                try
                {
                    handle.Release();
                }
                finally
                {
                    try
                    {
                        await complete();
                    }
                    catch (Exception ex)
                    {
                        _Log.Error("Completing calibration failed. Ex={Exception}", ex.ToString());
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _UpdateInProgress, 0);
                    }
                }
            }
        });
    }

    private sealed class ScopedOutput(CalibrationSession session) : IUpdateTableOutputPort, IUpdateTableSceneOutputPort
    {
        public Task ReportSuccess(TableConfiguration config) => session.IsActive() ? session.TableOutput.ReportSuccess(config) : Task.CompletedTask;
        public Task ReportSuccess() => session.IsActive() ? session.SceneOutput.ReportSuccess() : Task.CompletedTask;
        Task IUpdateTableOutputPort.ReportFailure(string reason) => session.IsActive() ? session.TableOutput.ReportFailure(reason) : Task.CompletedTask;
        Task IUpdateTableSceneOutputPort.ReportFailure(string reason) => session.IsActive() ? session.SceneOutput.ReportFailure(reason) : Task.CompletedTask;
    }
}
