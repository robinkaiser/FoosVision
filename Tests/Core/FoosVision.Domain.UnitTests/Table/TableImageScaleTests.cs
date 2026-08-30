// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;

namespace FoosVision.Domain.UnitTests.Table;

public class TableImageScaleTests
{
    [Fact]
    public void From_uses_center_axis_lengths_from_table_configuration()
    {
        TableImageScale scale = TableImageScale.From(TableConfig.Config);

        Assert.Equal(1360.0, scale.GoalAxisLengthPx, precision: 3);
        Assert.Equal(660.0, scale.SideAxisLengthPx, precision: 3);
    }

    [Fact]
    public void Convert_distances_from_pixels_to_millimeters()
    {
        TableImageScale scale = TableImageScale.From(TableConfig.Config);

        Assert.Equal(TableImageScale.GoalAxisLengthMm / 2.0, scale.ConvertGoalAxisDistancePxToMm(680.0), precision: 3);
        Assert.Equal(TableImageScale.SideAxisLengthMm / 2.0, scale.ConvertSideAxisDistancePxToMm(330.0), precision: 3);
    }

    [Fact]
    public void Convert_speeds_from_pixels_per_second_to_kilometers_per_hour()
    {
        TableImageScale scale = TableImageScale.From(TableConfig.Config);

        Assert.Equal(2.951, scale.ConvertGoalAxisSpeedPxPerSToKmh(1000.0), precision: 3);
        Assert.Equal(3.72, scale.ConvertSideAxisSpeedPxPerSToKmh(1000.0), precision: 3);
    }
}
