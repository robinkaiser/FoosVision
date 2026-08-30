// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.Common;

namespace FoosVision.Vision.TableConfig;

public static class TableBarsFactory
{
    public static TableBars From(IEnumerable<Bar> bars, Line upperLine, Line lowerLine)
    {
        var byType = bars.ToDictionary(b => b.Type);

        Bar MapByType(BarType t) => Map(byType[t], upperLine, lowerLine);

        return new TableBars(
            MapByType(BarType.A1),
            MapByType(BarType.A2),
            MapByType(BarType.B3),
            MapByType(BarType.A5),
            MapByType(BarType.B5),
            MapByType(BarType.A3),
            MapByType(BarType.B2),
            MapByType(BarType.B1));
    }

    private static Bar Map(Bar bar, Line upperLine, Line lowerLine)
    {
        return new Bar(
            bar.Type,
            Map(bar.Left, upperLine, lowerLine),
            Map(bar.Center, upperLine, lowerLine),
            Map(bar.Right, upperLine, lowerLine));
    }

    private static Line Map(Line barLine, Line upperLine, Line lowerLine)
    {
        bool hasUpper = Geometry.TryIntersect(barLine, upperLine, out Point p0);
        bool hasLower = Geometry.TryIntersect(barLine, lowerLine, out Point p1);

        if (!hasUpper || !hasLower)
        {
            return barLine;
        }

        return new Line(p0, p1);
    }
}
