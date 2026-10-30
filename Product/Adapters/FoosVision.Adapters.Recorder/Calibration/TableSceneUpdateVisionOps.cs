// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.UseCases.Calibration.Ports;

namespace FoosVision.Adapters.Recorder.Calibration;

public class TableSceneUpdateVisionOps : ITableSceneUpdateVisionOps
{
    private readonly ITableSceneUpdater _TableSceneUpdater;
    private readonly IFrameHandle _Frame;
    private readonly Func<bool> _IsActive;

    public TableSceneUpdateVisionOps(ITableSceneUpdater tableSceneUpdater, IFrameHandle frameHandle, Func<bool>? isActive = null)
    {
        _TableSceneUpdater = tableSceneUpdater;
        _Frame = frameHandle;
        _IsActive = isActive ?? (() => true);
    }

    public void Update(TableConfiguration tableConfig, Option<Point> ballPosition)
    {
        if (!_IsActive()) throw new OperationCanceledException();
        _TableSceneUpdater.Update(_Frame.BufferRGBA8888, tableConfig, ballPosition);
    }
}
