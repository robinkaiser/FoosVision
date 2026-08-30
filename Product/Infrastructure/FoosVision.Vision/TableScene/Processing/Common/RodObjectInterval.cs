// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Vision.TableScene.Processing.Common;

public readonly record struct RodObjectInterval(int StartIndex, int EndIndex, double Score)
{
    public int Length => EndIndex - StartIndex + 1;
}

public readonly record struct RodObjectIntervalScan(RodObjectInterval[] Intervals, int Count);
