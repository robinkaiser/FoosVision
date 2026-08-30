// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Vision.TableConfig.Processing.HoughLines;

public readonly record struct LineCoverageScore(
    int SupportedBins,
    int BinCount,
    int LongestSupportedRun,
    int EdgePixelCount)
{
    public double Coverage => BinCount == 0 ? 0.0 : (double)SupportedBins / BinCount;

    public double LongestRunCoverage => BinCount == 0 ? 0.0 : (double)LongestSupportedRun / BinCount;
}
