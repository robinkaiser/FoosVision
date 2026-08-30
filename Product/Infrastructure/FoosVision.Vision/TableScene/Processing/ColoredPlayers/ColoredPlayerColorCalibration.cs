// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Runtime.CompilerServices;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Vision.TableScene.Processing.ColoredPlayers;

public record ColoredPlayerColorCalibration(TeamColorCalibration TeamA, TeamColorCalibration TeamB);

public record TeamColorCalibration(
    Team Team,
    int IntervalCount,
    int ChromaticSampleCount,
    ChromaticColorModel? ColorModel)
{
    public bool HasColorModel => ColorModel is not null;
}

public record ChromaticColorModel(
    int CenterCb,
    int CenterCr,
    double Radius,
    int MinimumChromaticDistance,
    int SampleCount)
{
    public double RadiusSquared { get; } = Radius * Radius;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Matches(int cb, int cr)
    {
        int dCb = cb - CenterCb;
        int dCr = cr - CenterCr;

        return ((dCb * dCb) + (dCr * dCr)) <= RadiusSquared;
    }
}
