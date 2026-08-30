// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;

namespace FoosVision.Ports.Vision;

public interface ITableSceneUpdater
{
    void Update(byte[] frameBufferRGBA8888, TableConfiguration tableConfig, Option<Point> ballPosition);
}
