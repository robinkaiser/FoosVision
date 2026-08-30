// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.UseCases.Calibration.Ports;

namespace FoosVision.UseCases.Calibration.UpdateTable;

public enum UpdateMode
{
    /// <summary>
    /// Old table configuration is deleted in advance.
    /// </summary>
    Reset,

    /// <summary>
    /// Valid table configuration must exist in advance.
    /// </summary>
    Update,
}

public record UpdateTableRequest(
    Frame Frame,
    ITableDetectionVisionOps Vision,
    UpdateMode Mode);

public interface IUpdateTableInputPort
{
    Task Handle(UpdateTableRequest request, IUpdateTableOutputPort output, CancellationToken ct);
}
