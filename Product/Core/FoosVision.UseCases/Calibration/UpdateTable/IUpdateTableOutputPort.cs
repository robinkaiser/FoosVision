// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;

namespace FoosVision.UseCases.Calibration.UpdateTable;

public interface IUpdateTableOutputPort
{
    Task ReportSuccess(TableConfiguration config);

    Task ReportFailure(string reason);
}
