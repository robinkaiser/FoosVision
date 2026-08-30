// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.UseCases.Calibration.Ports;

namespace FoosVision.Adapters.Recorder.Game.Orchestration;

public class TableDetectionVisionOps : ITableDetectionVisionOps
{
    private readonly ITableConfigFinder _TableFinder;
    private readonly IFrameHandle _Frame;

    public TableDetectionVisionOps(ITableConfigFinder tableFinder, IFrameHandle frameHandle)
    {
        _TableFinder = tableFinder;
        _Frame = frameHandle;
    }

    public Option<TableConfiguration> Detect()
    {
        return _TableFinder.Detect(_Frame.BufferRGBA8888);
    }
}
