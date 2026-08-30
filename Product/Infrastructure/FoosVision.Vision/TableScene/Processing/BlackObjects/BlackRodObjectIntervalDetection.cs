// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableScene.Processing.Common;

namespace FoosVision.Vision.TableScene.Processing.BlackObjects;

public record BlackRodObjectIntervalDetection(
    IReadOnlyList<RodBlackObjectIntervals> Rods,
    BlackObjectRule Rule);

public record RodBlackObjectIntervals(
    BarType BarType,
    IReadOnlyList<RodObjectInterval> Intervals,
    BlackSideBandSampleProfile SampleProfile);

public record BlackSideBandSampleProfile(
    int[] X,
    int[] Y,
    bool[] Ignored,
    int[] LeftY,
    int[] RightY,
    bool[] LeftValid,
    bool[] RightValid,
    bool[] Matches,
    int Count);

public readonly record struct BlackObjectRule(
    int MaximumObjectY,
    double ObjectPercentile,
    int PercentileObjectY,
    double SearchMinimumPercentile,
    double SearchMaximumPercentile,
    int SearchMinimumY,
    int SearchMaximumY,
    int SideBandOffset,
    int SideBandWidth,
    int MinimumRunLength,
    int MaximumGapLength);

public record BlackObjectIntervalDetectionOptions(
    int SideBandOffset = 2,
    int SideBandWidth = 5,
    double OneColoredTeamObjectPercentile = 0.12,
    double TwoColoredTeamsObjectPercentile = 0.07,
    double OneColoredTeamSearchMinimumPercentile = 0.06,
    double OneColoredTeamSearchMaximumPercentile = 0.2,
    double TwoColoredTeamsSearchMinimumPercentile = 0.03,
    double TwoColoredTeamsSearchMaximumPercentile = 0.12,
    int MinimumRunLength = 3,
    int MaximumGapLength = 2);
