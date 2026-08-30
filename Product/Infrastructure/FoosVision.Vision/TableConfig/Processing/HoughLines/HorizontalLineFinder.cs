#pragma warning disable IDE0073

// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser
// SPDX-FileCopyrightText: 2005-2008 Andrew Kirillov <andrew.kirillov@aforgenet.com>
//
// This file contains modifications and is based on:
// https://github.com/accord-net/framework/blob/development/Sources/Accord.Imaging/AForge.Imaging/HoughLineTransformation.cs
//
// The upstream project is distributed under LGPL 2.1
//
// Original header:
//
// AForge Image Processing Library
// AForge.NET framework
// http://www.aforgenet.com/framework/
//
// Copyright © Andrew Kirillov, 2005-2009
// andrew.kirillov@aforgenet.com

#pragma warning restore IDE0073

namespace FoosVision.Vision.TableConfig.Processing.HoughLines;

public unsafe class HorizontalLineFinder : LineFinder
{
    public HorizontalLineFinder(int width, int height, int startAngle, int endAngle, double step)
        : base(width, height, startAngle, endAngle, step)
    {
    }

    public override int GetLines(int* pAccumulator, int mergeRange,
        HoughLine* pTempLines, HoughLine* pOutLines, int minR, int maxR, int minTheta, int maxTheta)
    {
        int lineCount = 0;
        HoughLine* pLines = mergeRange <= 0 ?
            pOutLines :
            pTempLines;

        var accumulatorThetaCount = AccumulatorThetaCount;
        var accumulatorRCount = AccumulatorRCount;
        var anglesSin = AnglesSin;
        var anglesCos = AnglesCos;
        var angleStep = AngleStep;
        var startAngle = StartAngle;
        var width = Width;

        for (int r = minR; r < maxR; r++)
        {
            int* pAcc = pAccumulator + (r * accumulatorThetaCount) + minTheta;

            for (int theta = minTheta; theta <= maxTheta; theta++)
            {
                if (lineCount >= MaxLineCount) break;

                int value = *pAcc;

                if (value == 0)
                {
                    pAcc++;
                    continue;
                }

                double x0 = 0;
                double x1 = width;
                double r2 = r - (accumulatorRCount / 2);

                double sin = anglesSin[theta];
                double cos = anglesCos[theta];

                if (sin == 0.0)
                {
                    pAcc++;
                    continue;
                }

                double y0 = (r2 - (x0 * cos)) / sin;
                double y1 = (r2 - (x1 * cos)) / sin;

                pLines->P0 = new(x0, y0);
                pLines->P1 = new(x1, y1);
                pLines->R = r;
                pLines->Angle = (theta * angleStep) + startAngle;
                pLines->Theta = theta;
                pLines->Accumulator = value;

                pLines++;
                lineCount++;
                pAcc++;
            }
        }

        if (mergeRange <= 0)
        {
            return lineCount;
        }

        var mergedLinesCount = MergeLines(mergeRange, pTempLines, lineCount, pOutLines,
            (HoughLine line) => (line.P0.Y + line.P1.Y) / 2);

        return mergedLinesCount;
    }
}
