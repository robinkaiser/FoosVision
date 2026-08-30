// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Calibration.UpdateTableScene;

public interface IUpdateTableSceneOutputPort
{
    Task ReportSuccess();

    Task ReportFailure(string reason);
}
