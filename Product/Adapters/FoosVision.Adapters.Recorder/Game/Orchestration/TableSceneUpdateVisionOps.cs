// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.UseCases.Calibration.Ports;

namespace FoosVision.Adapters.Recorder.Game.Orchestration;

public class TableSceneUpdateVisionOps : ITableSceneUpdateVisionOps
{
    private readonly ITableSceneUpdater _TableSceneUpdater;
    private readonly IFrameHandle _Frame;

    public TableSceneUpdateVisionOps(ITableSceneUpdater tableSceneUpdater, IFrameHandle frameHandle)
    {
        _TableSceneUpdater = tableSceneUpdater;
        _Frame = frameHandle;
    }

    public void Update(TableConfiguration tableConfig, Option<Point> ballPosition)
    {
        _TableSceneUpdater.Update(_Frame.BufferRGBA8888, tableConfig, ballPosition);
    }
}
