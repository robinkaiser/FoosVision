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

namespace FoosVision.Vision.Common.Processing;

public static class CannyEdgeDetectionLookup
{
    public const int MaxSquaredGradients = 2080800;
    public const int MaxOrients = 2040;
    public const int MaxOrientsHalf = 1020;

    public static readonly float[] GradientLut = CreateGradientLut();
    public static readonly byte[,] OrientLut = CreateOrientLut();

    private static float[] CreateGradientLut()
    {
        var gradientLut = new float[MaxSquaredGradients];

        for (int i = 0; i < MaxSquaredGradients; i++)
        {
            gradientLut[i] = (float)Math.Sqrt(i);
        }

        return gradientLut;
    }

    private static byte[,] CreateOrientLut()
    {
        var orientLut = new byte[MaxOrients, MaxOrients];

        double orientation;
        double toAngle = 180.0 / Math.PI;

        for (int xx = 0; xx < MaxOrients; xx++)
        {
            for (int yy = 0; yy < MaxOrients; yy++)
            {
                int x = xx - MaxOrientsHalf;
                int y = yy - MaxOrientsHalf;

                // --- get orientation
                if (x == 0)
                {
                    // can not divide by zero
                    orientation = (y == 0) ? 0 : 90;
                }
                else
                {
                    double div = (double)y / x;

                    // handle angles of the 2nd and 4th quads
                    if (div < 0)
                    {
                        orientation = 180 - (Math.Atan(-div) * toAngle);
                    }

                    // handle angles of the 1st and 3rd quads
                    else
                    {
                        orientation = Math.Atan(div) * toAngle;
                    }

                    // get closest angle from 0, 45, 90, 135 set
                    if (orientation < 22.5)
                        orientation = 0;
                    else if (orientation < 67.5)
                        orientation = 45;
                    else if (orientation < 112.5)
                        orientation = 90;
                    else if (orientation < 157.5)
                        orientation = 135;
                    else orientation = 0;
                }

                // save orientation
                orientLut[xx, yy] = (byte)orientation;
            }
        }

        return orientLut;
    }
}
