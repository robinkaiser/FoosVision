// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;

namespace FoosVision.Domain.UnitTests;

public static class TableConfig
{
    static TableConfig()
    {
        var types = Enum.GetValues<BarType>();
        Dictionary<BarType, Bar> bars = [];

        /*
           (120,120)  +----------------------------------------------------+ (1480,120)
                      |      |      |      |      |      |      |      |
                      |      |      |      X      X      |      |      |
                      |      |      X      |      |      X      |      |
                      |      X      |      X      X      |      X      |
                      |      |      |      |      |      |      |      |
                      X      |      X      X      X      X      |      X
                      |      |      |      |      |      |      |      |
                      |      X      |      X      X      |      X      |
                      |      |      X      |      |      X      |      |
                      |      |      |      X      X      |      |      |
                      |      |      |      |      |      |      |      |
           (120,780)  +----------------------------------------------------+ (1480,780)
                      A      A      B      A      B      A      B      B
                      1      2      3      5      5      3      2      1
                     100    300    500    700    900   1100   1300   1500

           A1:  100 (  80 -  120)
           A2:  300 ( 280 -  320)
           B3:  500 ( 480 -  520)
           A5:  700 ( 680 -  720)
           B5:  900 ( 880 -  920)
           A3: 1100 (1080 - 1120)
           B2: 1300 (1280 - 1320)
           B1: 1500 (1480 - 1520)
        */

        int x = 100;

        foreach (var type in types)
        {
            Bar bar = new(
                type,
                new Line(new(x - 20, 0), new(x - 20, 1080)),
                new Line(new(x, 0), new(x, 1080)),
                new Line(new(x + 20, 0), new(x + 20, 1080)));

            bars.Add(type, bar);
            x += 200;
        }

        var leftBar = bars[BarType.A1];
        var rightBar = bars[BarType.B1];

        double upperBorderLeftY = 120;
        double upperBorderRightY = 120;
        double lowerBorderLeftY = 780;
        double lowerBorderRightY = 780;

        Config = new(
            new PlayingField(
                new Trapezium(
                    new Point(leftBar.Right.P0.X, upperBorderLeftY),
                    new Point(rightBar.Left.P0.X, upperBorderRightY),
                    new Point(leftBar.Right.P1.X, lowerBorderLeftY),
                    new Point(rightBar.Left.P1.X, lowerBorderRightY)),
                new TableBars(
                    bars[BarType.A1],
                    bars[BarType.A2],
                    bars[BarType.B3],
                    bars[BarType.A5],
                    bars[BarType.B5],
                    bars[BarType.A3],
                    bars[BarType.B2],
                    bars[BarType.B1]),
                Occlusions: []),
            new PlayerColors(0xFFFF0000, 0xFF0000FF),
            BallColor.Unknown
        );
    }

    public static TableConfiguration Config { get; private set; }
}
