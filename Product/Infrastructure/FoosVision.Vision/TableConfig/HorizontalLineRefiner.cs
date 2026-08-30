// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Common.Types;
using FoosVision.Vision.TableConfig.Processing.HoughLines;

namespace FoosVision.Vision.TableConfig;

internal class HorizontalLineRefiner
{
    private const int _HalfHBorderHeightTolerance = 50;
    private const double _HalfHBorderdAngleTolerance = 5.0;
    private const double _MaxHBorderdMidYDifference = 20.0;

    private static readonly Source _Log = new("Vision.HorizontalLineRefiner");

    private readonly int _Width;
    private readonly int _Height;
    private readonly HorizontalLineFinder _HorizontalFineLineFinder;
    private readonly HoughLine[] _HoughLinesFineTemp;
    private readonly HoughLine[] _HoughLinesFine;

    public HorizontalLineRefiner(int width, int height)
    {
        _Width = width;
        _Height = height;
        _HorizontalFineLineFinder = new HorizontalLineFinder(width, height, 80, 100, 0.1);
        _HoughLinesFineTemp = new HoughLine[LineFinder.MaxLineCount];
        _HoughLinesFine = new HoughLine[LineFinder.MaxLineCount];
    }

    public HoughLine? Refine(byte[] y8CannyBuffer, HoughLine line, string operationName, Rectangle? clipRectangle = null)
    {
        var fineLinesCount = GetFineLines(y8CannyBuffer, line, clipRectangle);
        if (fineLinesCount == 0)
        {
            _Log.Warning(
                "{OperationName} failed: no fine lines found. RoughLineP0=({P0X},{P0Y}) RoughLineP1=({P1X},{P1Y})",
                operationName,
                line.P0.X,
                line.P0.Y,
                line.P1.X,
                line.P1.Y);
            return null;
        }

        var medianLine = _HoughLinesFine.Take(fineLinesCount)
            .OrderBy(l => l.Angle)
            .ElementAt(fineLinesCount / 2);

        HoughLine resultLine = new()
        {
            P0 = medianLine.P0,
            P1 = medianLine.P1,
        };

        return resultLine;
    }

    private int GetFineLines(byte[] y8CannyBuffer, HoughLine line, Rectangle? clipRectangle)
    {
        int x0 = (int)line.P0.X;
        int y0 = (int)Math.Min(line.P0.Y, line.P1.Y) - _HalfHBorderHeightTolerance;
        y0 = Math.Max(y0, 0);

        int x1 = (int)line.P1.X;
        int y1 = (int)Math.Max(line.P0.Y, line.P1.Y) + _HalfHBorderHeightTolerance;
        y1 = Math.Min(y1, _Height);

        int width = x1 - x0;
        int height = y1 - y0;
        if (!FieldDetectorMath.TryClampRectangle(_Width, _Height, x0, y0, width, height, out Rectangle rect))
        {
            _Log.Warning(
                "GetFineLines skipped: invalid fine rect. Raw=({X},{Y},{Width},{Height}) LineP0=({P0X},{P0Y}) LineP1=({P1X},{P1Y})",
                x0,
                y0,
                width,
                height,
                line.P0.X,
                line.P0.Y,
                line.P1.X,
                line.P1.Y);
            return 0;
        }

        if (clipRectangle.HasValue)
        {
            rect = Rectangle.Intersect(rect, clipRectangle.Value);

            if (rect.IsEmpty)
            {
                _Log.Warning(
                    "GetFineLines skipped: empty clipped fine rect. LineP0=({P0X},{P0Y}) LineP1=({P1X},{P1Y})",
                    line.P0.X,
                    line.P0.Y,
                    line.P1.X,
                    line.P1.Y);
                return 0;
            }
        }

        var fineLinesTempCount = _HorizontalFineLineFinder.Find(
            y8CannyBuffer, rect, 0.5, 0, 0, 0, _HoughLinesFineTemp,
            line.R - _HalfHBorderHeightTolerance, line.R + _HalfHBorderHeightTolerance,
            line.Angle - _HalfHBorderdAngleTolerance, line.Angle + _HalfHBorderdAngleTolerance);
        if (fineLinesTempCount >= LineFinder.MaxLineCount)
        {
            _Log.Warning("GetFineLines failed: fine line result hit the buffer limit. Limit={Limit}", LineFinder.MaxLineCount);
            return 0;
        }

        int fineLinesCount = 0;
        double expectedMidPointY = (line.P0.Y + line.P1.Y) / 2.0;

        for (int i = 0; i < fineLinesTempCount; i++)
        {   // Other border lines could again pop up during the fine estimation.
            // So check if the fine candidate mid point is near the rough estimation.
            double midPointY = (_HoughLinesFineTemp[i].P0.Y + _HoughLinesFineTemp[i].P1.Y) / 2.0;

            if (Math.Abs(midPointY - expectedMidPointY) <= _MaxHBorderdMidYDifference)
            {
                _HoughLinesFine[fineLinesCount] = _HoughLinesFineTemp[i];
                fineLinesCount++;
            }
        }

        return fineLinesCount;
    }
}
