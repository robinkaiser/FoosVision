// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableScene.Processing.Common;

namespace FoosVision.Vision.TableScene.Processing.ColoredPlayers;

public record ColoredRodObjectIntervalDetection(IReadOnlyList<RodColoredObjectIntervals> Rods);

public record RodColoredObjectIntervals(
    BarType BarType,
    IReadOnlyList<RodObjectInterval> Intervals,
    RodSampleProfile SampleProfile,
    RodEdgeScoreProfile EdgeScoreProfile);

public record RodSampleProfile(int[] X, int[] Y, bool[] Occluded, ColorFeature[] Features, int Count);

public record RodEdgeScoreProfile(double[] Scores, double MinimumScore, int Count);

public record ColoredRodObjectIntervalDetectionOptions(
    int EdgeWindowLength = 30,
    double EdgeMinimumScoreRatio = 0.3,
    int EdgePeakNeighborhood = 25,
    int EdgePairMaximumDistance = 60);
