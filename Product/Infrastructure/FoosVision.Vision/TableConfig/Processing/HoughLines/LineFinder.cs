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

using FoosVision.Common.Types;

namespace FoosVision.Vision.TableConfig.Processing.HoughLines;

public record struct HoughLine(Point P0, Point P1, int R, double Angle, int Theta, int Accumulator);

public unsafe abstract class LineFinder
{
    /// <summary>
    /// Maximum number of lines to be processed.
    /// </summary>
    public const int MaxLineCount = 3000;

    private readonly int _Width;
    private readonly int _Height;
    private readonly int _StartAngle;
    private readonly int _EndAngle;
    private readonly double _AngleStep;
    private readonly int _MaxDistanceFromOrigin;
    private readonly double[] _Angles;
    private readonly double[] _AnglesSin;
    private readonly double[] _AnglesCos;
    private readonly int[,] _Accumulator;
    private readonly HoughLine[] _TempLines;

    public LineFinder(int width, int height, int startAngle, int endAngle, double step = 0.1)
    {
        _Width = width;
        _Height = height;
        _StartAngle = startAngle;
        _EndAngle = endAngle;
        _AngleStep = step;

        int angleCount = (int)(((_EndAngle - _StartAngle) / _AngleStep) + 1);
        _Angles = new double[angleCount];
        _AnglesSin = new double[angleCount];
        _AnglesCos = new double[angleCount];

        for (int i = 0; i < angleCount; i++)
        {
            double degrees = _StartAngle + (i * _AngleStep);
            double radians = degrees * Math.PI / 180;
            _Angles[i] = radians;
            _AnglesSin[i] = Math.Sin(radians);
            _AnglesCos[i] = Math.Cos(radians);
        }

        _MaxDistanceFromOrigin = (int)Math.Ceiling(Math.Sqrt((_Width * _Width) + (_Height * _Height)));

        // Distance ranges from -maxDist to +maxDist
        _Accumulator = new int[_MaxDistanceFromOrigin * 2, angleCount];

        _TempLines = new HoughLine[MaxLineCount];
    }

    protected int Width => _Width;
    protected int Height => _Height;
    protected int StartAngle => _StartAngle;
    protected double AngleStep => _AngleStep;
    protected double[] AnglesSin => _AnglesSin;
    protected double[] AnglesCos => _AnglesCos;
    protected int AccumulatorRCount => _Accumulator.GetLength(0);
    protected int AccumulatorThetaCount => _Accumulator.GetLength(1);

    /// <summary>
    /// Find line in the input edge image.
    /// </summary>
    /// <param name="inputY8EdgeImage">Input edge image.</param>
    /// <param name="rect">Rectangle of the image to be processed.</param>
    /// <param name="thresholdAccumulatorBinRatio">Threshold on individual accumulator bins [0, 1].</param>
    /// <param name="neighborsR">R-neighborhood for non-maximum suppression.</param>
    /// <param name="neighborsTheta">theta-neighborhood for non-maximum suppression.</param>
    /// <param name="mergeRangePixel">Final step merging range for lines, depending on the lines mid-point.</param>
    /// <param name="outLines">Array must be of size MaxLines.</param>
    /// <param name="minR">Minimum R to be considered when extracting lines from accumulator. No restrictions apply if not specified.</param>
    /// <param name="maxR">Maximum R to be considered when extracting lines from accumulator. No restrictions apply if not specified.</param>
    /// <param name="minAngle">Minimum angle to be considered when extracting lines from accumulator. No restrictions apply if not specified.</param>
    /// <param name="maxAngle">Maximum angle to be considered when extracting lines from accumulator. No restrictions apply if not specified.</param>
    /// <returns>Number of lines found.</returns>
    public int Find(byte[] inputY8EdgeImage, Rectangle rect, double thresholdAccumulatorBinRatio,
        int neighborsR, int neighborsTheta, int mergeRangePixel, HoughLine[] outLines,
        int minR = 0, int maxR = int.MaxValue, double minAngle = double.MinValue, double maxAngle = double.MaxValue)
    {
        Array.Clear(_Accumulator);

        minR = Math.Max(minR, 0);
        maxR = Math.Min(maxR, _Accumulator.GetLength(0));
        minAngle = Math.Max(minAngle, _StartAngle);
        maxAngle = Math.Min(maxAngle, _EndAngle);
        int minTheta = (int)((minAngle - _StartAngle) / _AngleStep);
        int maxTheta = (int)((maxAngle - _StartAngle) / _AngleStep);

        minTheta = Math.Clamp(minTheta, 0, AccumulatorThetaCount - 1);
        maxTheta = Math.Clamp(maxTheta, 0, AccumulatorThetaCount - 1);

        if (minR >= maxR ||
            minTheta > maxTheta)
        {
            return 0;
        }

        fixed (byte* pInY8Edge = inputY8EdgeImage)
        fixed (int* pAccumulator = _Accumulator)
        fixed (double* pInAngles = _Angles)
        fixed (HoughLine* pTempLines = _TempLines)
        fixed (HoughLine* pOutLines = outLines)
        {
            int maxAcc = Y8ToHoughAccumulator(pInY8Edge, pAccumulator, pInAngles, rect, minTheta, maxTheta);
            int thresholdAccumulatorBin = (int)(thresholdAccumulatorBinRatio * maxAcc);

            ApplymNonMaximumSuppressionAndThresholdingOnAccumulator(
                pAccumulator, thresholdAccumulatorBin, neighborsR, neighborsTheta, minR, maxR, minTheta, maxTheta);

            var lineCount = GetLines(pAccumulator, mergeRangePixel, pTempLines, pOutLines, minR, maxR, minTheta, maxTheta);
            return lineCount;
        }
    }

    public abstract int GetLines(int* pAccumulator, int mergeRange,
        HoughLine* pTempHoughLines, HoughLine* pOutHoughLines, int minR, int maxR, int thetaMin, int thetaMax);

    protected int MergeLines(int range, HoughLine* pInLines, int lineCount, HoughLine* pOutLines,
        Func<HoughLine, double> GetMergeCriteria)
    {
        if (lineCount == 0)
        {
            return 0;
        }

        if (lineCount == 1)
        {
            AddMergedLine(pInLines, 1, pOutLines);
            return 1;
        }

        HoughLine* pLine0 = pInLines;
        HoughLine* pLine1 = pLine0 + 1;
        HoughLine* pEnd = pInLines + lineCount;

        int mergedLineCount = 0;
        double pos = GetMergeCriteria(*pLine0);

        while (pLine1 < pEnd)
        {
            double newPos = GetMergeCriteria(*pLine1);

            if (newPos <= pos + range)
            {   // Merge i into i0
                pos = newPos;
            }
            else
            {   // New line
                AddMergedLine(pLine0, (int)(pLine1 - pLine0), pOutLines);
                mergedLineCount++;
                pOutLines++;

                pos = newPos;
                pLine0 = pLine1;
            }

            pLine1++;
        }

        AddMergedLine(pLine0, (int)(pLine1 - pLine0), pOutLines);
        mergedLineCount++;

        return mergedLineCount;
    }

    private int Y8ToHoughAccumulator(byte* pInY8Edge, int* pAccumulator, double* pInAngles,
        Rectangle rect, int minTheta, int maxTheta)
    {
        int startX = rect.X;
        int startY = rect.Y;

        // Stop is exclusive
        int stopX = rect.X + rect.Width;
        int stopY = rect.Y + rect.Height;

        // TODO: Beware stride assumption
        int srcStride = _Width;
        int srcOffset = srcStride - (stopX - startX);

        int fullTheta = _Angles.Length;
        int maxAcc = 0;

        for (int y = startY; y < stopY; y++)
        {
            byte* pSrc = pInY8Edge + (srcStride * y) + startX;

            for (int x = startX; x < stopX; x++)
            {
                if (*pSrc > 0)
                {
                    for (int theta = minTheta; theta <= maxTheta; theta++)
                    {   // Calculate distance from origin
                        double r = (x * _AnglesCos[theta]) + (y * _AnglesSin[theta]);
                        int d = (int)(r + _MaxDistanceFromOrigin + 0.5);

                        int* pAcc = pAccumulator + (d * fullTheta) + theta;
                        (*pAcc)++;

                        if (*pAcc > maxAcc)
                        {
                            maxAcc = *pAcc;
                        }
                    }
                }

                pSrc++;
            }
        }

        return maxAcc;
    }

    private void ApplymNonMaximumSuppressionAndThresholdingOnAccumulator(
        int* pAccumulator, int threshold, int nbR, int nbTheta, int minR, int maxR, int minTheta, int maxTheta)
    {
        int fullTheta = _Accumulator.GetLength(1);
        bool skipMaximumSuppression = nbR == 0 && nbTheta == 0;

        for (int r = minR; r < maxR; r++)
        {
            int* pAcc = pAccumulator + (r * fullTheta) + minTheta;

            for (int theta = minTheta; theta <= maxTheta; theta++)
            {
                if (*pAcc <= threshold)
                {
                    *pAcc = 0;
                    pAcc++;
                    continue;
                }

                if (skipMaximumSuppression)
                {
                    pAcc++;
                    continue;
                }

                bool isSuppressed = false;

                int rr_start = Math.Max(r - nbR, 0);
                int rr_end = Math.Min(r + nbR, maxR - 1);
                int tt_start = Math.Max(theta - nbTheta, 0);
                int tt_end = Math.Min(theta + nbTheta, fullTheta - 1);

                for (int rr = rr_start; rr <= rr_end; rr++)
                {
                    int* pAcc2 = pAccumulator + (rr * fullTheta) + tt_start;

                    for (int tt = tt_start; tt <= tt_end; tt++)
                    {
                        if (rr == r && tt == theta)
                        {   // Skip the center point
                            pAcc2++;
                            continue;
                        }

                        if (*pAcc <= *pAcc2)
                        {
                            *pAcc = 0;
                            isSuppressed = true;
                            break;
                        }

                        pAcc2++;
                    }

                    if (isSuppressed) break;
                }

                pAcc++;
            }
        }
    }

    private static void AddMergedLine(HoughLine* lines, int count, HoughLine* pMergedLine)
    {
        if (count <= 1)
        {
            pMergedLine->P0 = lines->P0;
            pMergedLine->P1 = lines->P1;
            pMergedLine->R = lines->R;
            pMergedLine->Angle = lines->Angle;
            pMergedLine->Theta = lines->Theta;
            pMergedLine->Accumulator = lines->Accumulator;
            return;
        }

        double x0 = lines->P0.X;
        double y0 = lines->P0.Y;
        double x1 = lines->P1.X;
        double y1 = lines->P1.Y;
        int r = lines->R;
        double angle = lines->Angle;
        int theta = lines->Theta;
        int acc = lines->Accumulator;

        lines++;

        for (int i = 1; i < count; i++)
        {
            x0 += lines->P0.X;
            y0 += lines->P0.Y;
            x1 += lines->P1.X;
            y1 += lines->P1.Y;
            r += lines->R;
            angle += lines->Angle;
            theta += lines->Theta;
            acc += lines->Accumulator;

            lines++;
        }

        x0 /= count;
        y0 /= count;
        x1 /= count;
        y1 /= count;
        r /= count;
        angle /= count;
        theta /= count;

        pMergedLine->P0 = new(x0, y0);
        pMergedLine->P1 = new(x1, y1);
        pMergedLine->R = r;
        pMergedLine->Angle = angle;
        pMergedLine->Theta = theta;
        pMergedLine->Accumulator = acc;
    }
}
