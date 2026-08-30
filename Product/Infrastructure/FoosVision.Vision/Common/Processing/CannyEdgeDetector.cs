#pragma warning disable IDE0073

// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser
// SPDX-FileCopyrightText: 2005-2008 Andrew Kirillov <andrew.kirillov@aforgenet.com>
//
// This file contains modifications and is based on:
// https://github.com/accord-net/framework/blob/development/Sources/Accord.Imaging/AForge.Imaging/Filters/Edge%20Detectors/CannyEdgeDetector.cs
//
// The upstream project is distributed under LGPL 2.1
//
// Original header:
//
// AForge Image Processing Library
// AForge.NET framework
//
// Copyright © Andrew Kirillov, 2005-2008
// andrew.kirillov@aforgenet.com
//
// Article by Bill Green was used as the reference
// http://www.pages.drexel.edu/~weg22/can_tut.html

#pragma warning restore IDE0073

using FoosVision.Common.Types;

namespace FoosVision.Vision.Common.Processing;

public record struct EdgePoint(int X, int Y);

public unsafe class CannyEdgeDetector
{
    private const int _MaxSquaredGradients = CannyEdgeDetectionLookup.MaxSquaredGradients;
    private const int _MaxOrients = CannyEdgeDetectionLookup.MaxOrients;
    private const int _MaxOrientsHalf = CannyEdgeDetectionLookup.MaxOrientsHalf;

    private const int _LowThreshold = 5;
    private const int _HighThreshold = 50;

    private readonly int _Width;
    private readonly float[] _GradientLut;
    private readonly byte[,] _OrientLut;

    private readonly float[] _Gradients;
    private readonly byte[] _Orients;
    private readonly byte[] _Output;

    public CannyEdgeDetector(int width, int height)
    {
        _Width = width;
        _GradientLut = CannyEdgeDetectionLookup.GradientLut;
        _OrientLut = CannyEdgeDetectionLookup.OrientLut;

        _Gradients = new float[width * height];
        _Orients = new byte[width * height];
        _Output = new byte[width * height];
    }

    public void Process(byte[] inputY8, byte[] outputY8, Rectangle rect)
    {
        fixed (byte* pInY8 = inputY8)
        fixed (byte* pOutY8 = outputY8)
        fixed (byte* pOrients = _Orients)
        fixed (float* pGradients = _Gradients)
        fixed (float* pGradLookup = _GradientLut)
        fixed (byte* pOrientsLookup = _OrientLut)
            Process(pInY8, pOutY8, rect, pOrients, pGradients, pGradLookup, pOrientsLookup);
    }

    public int Process(byte[] inputY8, Rectangle rect, EdgePoint[] outPoints)
    {
        Process(inputY8, _Output, rect);

        int pointCount = 0;

        fixed (byte* pOut = _Output)
        {
            int startX = rect.X;
            int startY = rect.Y;

            // Stop is exclusive
            int stopX = rect.X + rect.Width;
            int stopY = rect.Y + rect.Height;

            // TODO: Beware stride assumption
            int stride = _Width;
            int offset = stride - (stopX - startX);

            byte* pEdge = pOut + (stride * startY) + startX;

            for (int row = startY; row < stopY; row++)
            {
                for (int col = startX; col < stopX; col++)
                {
                    if (*pEdge != 0)
                    {
                        outPoints[pointCount] = new(col, row);
                        pointCount++;
                    }

                    pEdge++;
                }

                pEdge += offset;
            }
        }

        return pointCount;
    }

    private void Process(byte* pInY8, byte* pOutY8, Rectangle rect,
        byte* pOrients, float* pGradients, float* pGradLookup, byte* pOrientsLookup)
    {   // Start coordinates: skip first row and column
        int startX = rect.X + 1;
        int startY = rect.Y + 1;

        // Stop coordinates: skip last row and column. stop is exclusive
        int stopX = rect.X + rect.Width - 1;
        int stopY = rect.Y + rect.Height - 1;

        // TODO: Beware stride assumption
        int dstStride = _Width;
        int srcStride = _Width;

        int dstOffset = dstStride - (stopX - startX);
        int srcOffset = srcStride - (stopX - startX);

        // pixel's value and gradients
        int gx, gy;
        float leftPixel = 0, rightPixel = 0;
        float maxGradient = float.NegativeInfinity;

        // allign pointer
        byte* src = pInY8 + (srcStride * startY) + startX;
        byte* pOrient = pOrients + (srcStride * startY) + startX;
        float* pGradient = pGradients + (srcStride * startY) + startX;

        // STEP 2 - calculate magnitude and edge orientation

        for (int y = startY; y < stopY; y++)
        {
            byte* pSrc1 = src + srcStride - 1;
            byte* pSrc2 = src + srcStride;
            byte* pSrc3 = src + srcStride + 1;
            byte* pSrc4 = src - 1;
            byte* pSrc6 = src + 1;
            byte* pSrc7 = src - srcStride - 1;
            byte* pSrc8 = src - srcStride;
            byte* pSrc9 = src - srcStride + 1;

            // for each pixel
            float* pGradientEnd = pGradient + (stopX - startX);

            while (pGradient < pGradientEnd)
            {
                gx = *pSrc9 + *pSrc3 - *pSrc7 - *pSrc1 + (2 * (*pSrc6 - *pSrc4));
                gy = *pSrc7 + *pSrc9 - *pSrc1 - *pSrc3 + (2 * (*pSrc8 - *pSrc2));

                // get gradient value
                *pGradient = *(pGradLookup + (gx * gx) + (gy * gy));

                // Runtime of step 2 could be reduced by approximation of gradient
                //gx = gx < 0 ? -gx : gx;
                //gy = gy < 0 ? -gy : gy;
                // *pGradient = gx + gy;

                if (*pGradient > maxGradient)
                {
                    maxGradient = *pGradient;
                }

                *pOrient = *(pOrientsLookup + ((gx + _MaxOrientsHalf) * _MaxOrients) + gy + _MaxOrientsHalf);

                pOrient++;
                pGradient++;

                pSrc1++;
                pSrc2++;
                pSrc3++;
                pSrc4++;
                pSrc6++;
                pSrc7++;
                pSrc8++;
                pSrc9++;
            }

            src += srcStride;
            pOrient += srcOffset;
            pGradient += srcOffset;
        }

        // STEP 3 - suppres non maximums

        // allign pointer
        byte* dst = pOutY8 + (dstStride * startY) + startX;
        pOrient = pOrients + (srcStride * startY) + startX;
        pGradient = pGradients + (srcStride * startY) + startX;

        for (int y = startY; y < stopY; y++)
        {
            byte* dstEnd = dst + (stopX - startX);

            while (dst < dstEnd)
            {
                // get two adjacent pixels
                switch (*pOrient)
                {
                    case 0:
                        leftPixel = *(pGradient - 1);
                        rightPixel = *(pGradient + 1);
                        break;
                    case 45:
                        leftPixel = *(pGradient + srcStride - 1);
                        rightPixel = *(pGradient - srcStride + 1);
                        break;
                    case 90:
                        leftPixel = *(pGradient + srcStride);
                        rightPixel = *(pGradient - srcStride);
                        break;
                    case 135:
                        leftPixel = *(pGradient + srcStride + 1);
                        rightPixel = *(pGradient - srcStride - 1);
                        break;
                }

                // compare current pixels value with adjacent pixels
                if ((*pGradient < leftPixel) || (*pGradient < rightPixel))
                {
                    *dst = 0;
                }
                else
                {
                    *dst = (byte)(*pGradient / maxGradient * 255);
                }

                dst++;
                pOrient++;
                pGradient++;
            }

            dst += dstOffset;
            pOrient += srcOffset;
            pGradient += srcOffset;
        }

        // STEP 4 - hysteresis
        dst = pOutY8 + (dstStride * startY) + startX;

        for (int y = startY; y < stopY; y++)
        {
            byte* dstEnd = dst + (stopX - startX);

            while (dst < dstEnd)
            {
                if (*dst < _LowThreshold)
                {   // non edge
                    *dst = 0;
                    dst++;
                    continue;
                }

                if (*dst >= _HighThreshold)
                {
                    dst++;
                    continue;
                }

                // check 8 neighboring pixels
                if ((dst[-1] < _HighThreshold) &&
                    (dst[1] < _HighThreshold) &&
                    (dst[-dstStride - 1] < _HighThreshold) &&
                    (dst[-dstStride] < _HighThreshold) &&
                    (dst[-dstStride + 1] < _HighThreshold) &&
                    (dst[dstStride - 1] < _HighThreshold) &&
                    (dst[dstStride] < _HighThreshold) &&
                    (dst[dstStride + 1] < _HighThreshold))
                {
                    *dst = 0;
                }

                dst++;
            }

            dst += dstOffset;
        }
    }
}
