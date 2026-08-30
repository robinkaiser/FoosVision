// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Common.Types;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.UseCases.Calibration.UpdateTable;
using FoosVision.UseCases.Installation.CompleteTableUpdate;

namespace FoosVision.Adapters.Recorder.Installation.Orchestration;

public class CalibrationCoordinator : ICalibrationCoordinator
{
    private static readonly Source _Log = new("Installation.CalibrationCoordinator");

    private readonly IUpdateTableInputPort _Update;
    private readonly IUpdateTableOutputPort _Presenter;
    private readonly ITableConfigFinder _TableConfigFinder;
    private readonly IFrameFeed _FrameFeed;
    private readonly ICompleteTableUpdateInputPort _CompleteTableUpdate;
    private int _UpdateInProgress;

    public CalibrationCoordinator(
        IUpdateTableInputPort updateTableConfig,
        IUpdateTableOutputPort updateTableConfigPresenter,
        ITableConfigFinder tableConfigFinder,
        IFrameFeed frameFeed,
        ICompleteTableUpdateInputPort completeTableUpdate)
    {
        _Update = updateTableConfig;
        _Presenter = updateTableConfigPresenter;
        _TableConfigFinder = tableConfigFinder;
        _FrameFeed = frameFeed;
        _CompleteTableUpdate = completeTableUpdate;
    }

    public Task RequestUpdate(Frame frame)
    {
        if (Interlocked.CompareExchange(ref _UpdateInProgress, 1, 0) != 0)
        {
            _Log.Warning("RequestUpdate skipped because a table update is already running. FrameId={FrameId}", frame.Id);
            return Task.CompletedTask;
        }

        if (!_FrameFeed.TryAcquireById(frame.Id, out var handle))
        {
            _Log.Warning("RequestUpdate - Frame not in pool. FrameId={FrameId}", frame.Id);
            Interlocked.Exchange(ref _UpdateInProgress, 0);
            return Task.CompletedTask;
        }

        _ = Task.Run(async () =>
        {
            ulong frameId = handle.Meta.Id;

            try
            {
                var visionOps = new TableDetectionVisionOps(_TableConfigFinder, handle);
                var request = new UpdateTableRequest(handle.Meta, visionOps, UpdateMode.Reset);
                await _Update.Handle(request, _Presenter, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _Log.Error("RequestUpdate - Failed. FrameId={FrameId} Ex={Exception}", frameId, ex.ToString());
                await _Presenter.ReportFailure($"Exception: {ex.Message}");
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
                    _Log.Error("RequestUpdate - Completing table update failed. FrameId={FrameId} Ex={Exception}", frameId, ex.ToString());
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
