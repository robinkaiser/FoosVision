// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;

namespace FoosVision.UseCases.Calibration.Ports;

public interface ITableDetectionVisionOps
{
    Option<TableConfiguration> Detect();
}
