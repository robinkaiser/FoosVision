// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Common.Types;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.UseCases.Calibration.UpdateTable;
using FoosVision.UseCases.Calibration.UpdateTableScene;
using FoosVision.UseCases.Game.CompleteTableSceneUpdate;
using FoosVision.UseCases.Game.CompleteTableUpdate;

namespace FoosVision.Adapters.Recorder.Game.Orchestration;

public class CalibrationCoordinator : ICalibrationCoordinator
{
    private static readonly Source _Log = new("CalibrationCoordinator");

    private readonly IUpdateTableInputPort _UpdateTable;
    private readonly IUpdateTableOutputPort _UpdateTableOutPort;
    private readonly ITableConfigFinder _TableConfigFinder;
    private readonly IUpdateTableSceneInputPort _UpdateTableScene;
    private readonly IUpdateTableSceneOutputPort _UpdateTableSceneOutPort;
    private readonly ITableSceneUpdater _TableSceneUpdater;
    private readonly IFrameFeed _FrameFeed;
    private readonly ICompleteTableUpdateInputPort _CompleteTableUpdate;
    private readonly ICompleteTableSceneUpdateInputPort _CompleteTableSceneUpdate;
    private int _UpdateInProgress;

    public CalibrationCoordinator(
        IUpdateTableInputPort updateTable,
        IUpdateTableOutputPort updateTableOutPort,
        ITableConfigFinder tableConfigFinder,
        IUpdateTableSceneInputPort updateTableScene,
        IUpdateTableSceneOutputPort updateTableSceneOutPort,
        ITableSceneUpdater tableSceneUpdater,
        IFrameFeed frameFeed,
        ICompleteTableUpdateInputPort completeTableUpdate,
        ICompleteTableSceneUpdateInputPort completeTableSceneUpdate)
    {
        _UpdateTable = updateTable;
        _UpdateTableOutPort = updateTableOutPort;
        _TableConfigFinder = tableConfigFinder;
        _UpdateTableScene = updateTableScene;
        _UpdateTableSceneOutPort = updateTableSceneOutPort;
        _TableSceneUpdater = tableSceneUpdater;
        _FrameFeed = frameFeed;
        _CompleteTableUpdate = completeTableUpdate;
        _CompleteTableSceneUpdate = completeTableSceneUpdate;
    }

    public Task RequestTableUpdate(Frame frame)
    {
        if (Interlocked.CompareExchange(ref _UpdateInProgress, 1, 0) != 0)
        {
            _Log.Warning("RequestTableUpdate skipped because a calibration update is already running. FrameId={FrameId}", frame.Id);
            return Task.CompletedTask;
        }

        if (!_FrameFeed.TryAcquireById(frame.Id, out var handle))
        {
            _Log.Warning("RequestTableUpdate - Frame not in pool. FrameId={FrameId}", frame.Id);
            Interlocked.Exchange(ref _UpdateInProgress, 0);
            return Task.CompletedTask;
        }

        _ = Task.Run(async () =>
        {
            ulong frameId = handle.Meta.Id;

            try
            {
                var visionOps = new TableDetectionVisionOps(_TableConfigFinder, handle);
                var request = new UpdateTableRequest(handle.Meta, visionOps, UpdateMode.Update);
                await _UpdateTable.Handle(request, _UpdateTableOutPort, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _Log.Error("RequestTableUpdate - Failed. FrameId={FrameId} Ex={Exception}", frameId, ex.ToString());
                await _UpdateTableOutPort.ReportFailure($"Exception: {ex.Message}");
            }
            finally
            {
                handle.Release();

                try
                {
                    await _CompleteTableUpdate.Handle(new CompleteTableUpdateRequest(), CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _Log.Error("RequestTableUpdate - Completing table update failed. FrameId={FrameId} Ex={Exception}", frameId, ex.ToString());
                }
                finally
                {
                    Interlocked.Exchange(ref _UpdateInProgress, 0);
                }
            }
        });

        return Task.CompletedTask;
    }

    public Task RequestTableSceneUpdate(Frame frame, Option<Point> ballPosition)
    {
        if (Interlocked.CompareExchange(ref _UpdateInProgress, 1, 0) != 0)
        {
            _Log.Warning("RequestTableSceneUpdate skipped because a calibration update is already running. FrameId={FrameId}", frame.Id);
            return Task.CompletedTask;
        }

        if (!_FrameFeed.TryAcquireById(frame.Id, out var handle))
        {
            _Log.Warning("RequestTableSceneUpdate - Frame not in pool. FrameId={FrameId}", frame.Id);
            Interlocked.Exchange(ref _UpdateInProgress, 0);
            return Task.CompletedTask;
        }

        _ = Task.Run(async () =>
        {
            ulong frameId = handle.Meta.Id;

            try
            {
                var visionOps = new TableSceneUpdateVisionOps(_TableSceneUpdater, handle);
                var request = new UpdateTableSceneRequest(handle.Meta, ballPosition, visionOps);
                await _UpdateTableScene.Handle(request, _UpdateTableSceneOutPort, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _Log.Error("RequestTableSceneUpdate - Failed. FrameId={FrameId} Ex={Exception}", frameId, ex.ToString());
                await _UpdateTableSceneOutPort.ReportFailure($"Unhandled exception: {ex.Message}");
            }
            finally
            {
                handle.Release();

                try
                {
                    await _CompleteTableSceneUpdate.Handle(new CompleteTableSceneUpdateRequest(), CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _Log.Error("RequestTableSceneUpdate - Completing table scene update failed. FrameId={FrameId} Ex={Exception}", frameId, ex.ToString());
                }
                finally
                {
                    Interlocked.Exchange(ref _UpdateInProgress, 0);
                }
            }
        });

        return Task.CompletedTask;
    }
}
