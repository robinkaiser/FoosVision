// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Setup.Orchestration;
using FoosVision.UseCases.Setup.ProcessFrame;

namespace FoosVision.Adapters.Recorder.Setup.Live;

public class FramePresenter : IProcessFrameOutputPort
{
    private readonly ICalibrationCoordinator _Calibration;

    public FramePresenter(ICalibrationCoordinator calibration)
    {
        _Calibration = calibration;
    }

    public async Task ReportProcessed(ProcessFrameResponse response)
    {
        if (response.RequestTableUpdate)
        {
            await _Calibration.RequestUpdate(response.Frame);
        }
    }

    public Task ReportSkipped(string reason)
    {
        return Task.CompletedTask;
    }
}
