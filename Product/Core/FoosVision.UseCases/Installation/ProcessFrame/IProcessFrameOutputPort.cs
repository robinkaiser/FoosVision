// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.UseCases.Installation.ProcessFrame;

public record ProcessFrameResponse(
    Frame Frame,
    bool RequestTableUpdate);

public interface IProcessFrameOutputPort
{
    Task ReportProcessed(ProcessFrameResponse response);

    Task ReportSkipped(string reason);
}
