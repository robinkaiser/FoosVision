// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Vision.TableScene.Processing.Common;

public record struct BPoint(int X, int Y)
{
    public int X { get; set; } = X;
    public int Y { get; set; } = Y;
}

public record struct BLine(BPoint P1, BPoint P2)
{
    public BPoint P1 { get; set; } = P1;
    public BPoint P2 { get; set; } = P2;
}

public static unsafe class Bresenham
{
    public static int GetPoints(BLine line, BPoint[] points)
    {
        fixed (BPoint* pPoints = points)
            return GetPoints(line, pPoints);
    }

    public static int GetPoints(BLine line, BPoint* points)
    {
        int x0 = line.P1.X;
        int y0 = line.P1.Y;
        int x1 = line.P2.X;
        int y1 = line.P2.Y;

        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);

        int count = 0;

        if (dx == 0)
        {
            // Vertical line
            for (int yy = y0; yy <= y1; yy++)
            {
                points->X = x0;
                points->Y = yy;
                points++;
                count++;
            }

            return count;
        }

        bool steep = dy > dx;

        if (steep)
        {
            (y0, x0) = (x0, y0);
            (y1, x1) = (x1, y1);
        }

        if (x0 > x1)
        {
            (x1, x0) = (x0, x1);
            (y1, y0) = (y0, y1);
        }

        int dX = x1 - x0;
        int dY = Math.Abs(y1 - y0);
        int err = dX / 2;
        int ystep = y0 < y1 ? 1 : -1;
        int y = y0;

        for (int x = x0; x <= x1; ++x)
        {
            if (steep)
            {
                points->X = y;
                points->Y = x;
            }
            else
            {
                points->X = x;
                points->Y = y;
            }

            points++;
            count++;

            err -= dY;

            if (err < 0)
            {
                y += ystep;
                err += dX;
            }
        }

        return count;
    }
}
