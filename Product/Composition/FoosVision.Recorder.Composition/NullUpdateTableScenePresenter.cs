// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.UseCases.Calibration.UpdateTableScene;

namespace FoosVision.Recorder.Composition;

internal class NullUpdateTableScenePresenter : IUpdateTableSceneOutputPort
{
    public Task ReportSuccess()
      => Task.CompletedTask;

    public Task ReportFailure(string reason)
        => Task.CompletedTask;
}
