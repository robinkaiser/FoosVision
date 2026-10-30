// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.UseCases.Calibration.Ports;

namespace FoosVision.Adapters.Recorder.Calibration;

public class TableDetectionVisionOps : ITableDetectionVisionOps
{
    private readonly ITableConfigFinder _TableFinder;
    private readonly IFrameHandle _Frame;
    private readonly Func<bool> _IsActive;

    public TableDetectionVisionOps(ITableConfigFinder tableFinder, IFrameHandle frameHandle, Func<bool>? isActive = null)
    {
        _TableFinder = tableFinder;
        _Frame = frameHandle;
        _IsActive = isActive ?? (() => true);
    }

    public Option<TableConfiguration> Detect()
    {
        if (!_IsActive()) throw new OperationCanceledException();
        Option<TableConfiguration> result = _TableFinder.Detect(_Frame.BufferRGBA8888);
        if (!_IsActive()) throw new OperationCanceledException();
        return result;
    }
}
