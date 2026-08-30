// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableScene.Processing.BlackObjects;
using FoosVision.Vision.ValidationTests.Utils;

namespace FoosVision.Vision.ValidationTests.TableScene.Diagnostics;

public static class BlackObjectIntervalOverlayWriter
{
    public static void Write(
        Rgba8888ImageData imageData,
        PlayingField field,
        BlackRodObjectIntervalDetection detection,
        string outputPath)
    {
        List<LineData> lines = [];
        Bar[] bars =
        [
            field.Bars.A1,
            field.Bars.A2,
            field.Bars.B3,
            field.Bars.A5,
            field.Bars.B5,
            field.Bars.A3,
            field.Bars.B2,
            field.Bars.B1
        ];

        for (int i = 0; i < field.Occlusions.Count; i++)
        {
            AddTrapezium(lines, field.Occlusions[i], ColorType.Green, 4);
        }

        for (int i = 0; i < bars.Length; i++)
        {
            Bar bar = bars[i];
            RodBlackObjectIntervals rod = detection.Rods[i];
            lines.Add(CreateLineData(bar.Center, ColorType.Blue, 2));
            AddSideBandLines(lines, bar, detection.Rule);

            foreach (var interval in rod.Intervals)
            {
                Point p0 = GetSamplePoint(rod.SampleProfile, interval.StartIndex);
                Point p1 = GetSamplePoint(rod.SampleProfile, interval.EndIndex);

                lines.Add(new(
                    p0.X,
                    p0.Y,
                    p1.X,
                    p1.Y,
                    ColorType.Pink,
                    StyleType.Solid,
                    8));
            }
        }

        ValidationImageUtils.WriteRGBA8888ImageWithLinesToFile(imageData, lines, outputPath);
    }

    private static void AddSideBandLines(List<LineData> lines, Bar bar, BlackObjectRule rule)
    {
        if (bar.Type != BarType.A1)
        {
            AddSideBandLine(lines, bar.Left, bar.Right, rule, ColorType.White);
        }

        if (bar.Type != BarType.B1)
        {
            AddSideBandLine(lines, bar.Right, bar.Left, rule, ColorType.White);
        }
    }

    private static void AddSideBandLine(List<LineData> lines, Line boundary, Line oppositeBoundary, BlackObjectRule rule, ColorType color)
    {
        Point p0 = OffsetOutward(boundary.P0, oppositeBoundary.P0, rule.SideBandOffset);
        Point p1 = OffsetOutward(boundary.P1, oppositeBoundary.P1, rule.SideBandOffset);

        lines.Add(new(
            p0.X,
            p0.Y,
            p1.X,
            p1.Y,
            color,
            StyleType.Dot,
            2));
    }

    private static Point OffsetOutward(Point boundary, Point oppositeBoundary, int offset)
    {
        double dx = boundary.X - oppositeBoundary.X;
        double dy = boundary.Y - oppositeBoundary.Y;
        double length = Math.Sqrt((dx * dx) + (dy * dy));

        if (length < 0.001)
        {
            return boundary;
        }

        return new(
            boundary.X + (dx / length * offset),
            boundary.Y + (dy / length * offset));
    }

    private static LineData CreateLineData(Line line, ColorType color, float thickness)
        => new(
            line.P0.X,
            line.P0.Y,
            line.P1.X,
            line.P1.Y,
            color,
            StyleType.Solid,
            thickness);

    private static void AddTrapezium(List<LineData> lines, Trapezium trapezium, ColorType color, float thickness)
    {
        lines.Add(CreateLineData(new(trapezium.UpperLeft, trapezium.UpperRight), color, thickness));
        lines.Add(CreateLineData(new(trapezium.UpperRight, trapezium.LowerRight), color, thickness));
        lines.Add(CreateLineData(new(trapezium.LowerRight, trapezium.LowerLeft), color, thickness));
        lines.Add(CreateLineData(new(trapezium.LowerLeft, trapezium.UpperLeft), color, thickness));
    }

    private static Point GetSamplePoint(BlackSideBandSampleProfile sampleProfile, int index)
    {
        int safeIndex = Math.Clamp(index, 0, sampleProfile.Count - 1);

        return new(sampleProfile.X[safeIndex], sampleProfile.Y[safeIndex]);
    }
}
