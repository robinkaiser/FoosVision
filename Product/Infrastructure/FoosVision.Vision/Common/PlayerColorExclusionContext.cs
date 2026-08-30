// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Runtime.CompilerServices;

namespace FoosVision.Vision.Common;

public readonly record struct PlayerColorExclusionContext(
    bool HasTeamA,
    BallDetectionColorModel TeamA,
    bool HasTeamB,
    BallDetectionColorModel TeamB)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MatchesRgb(int r, int g, int b)
    {
        int cb = 128 + (((-43 * r) - (85 * g) + (128 * b)) >> 8);
        int cr = 128 + (((128 * r) - (107 * g) - (21 * b)) >> 8);

        return Matches(cb, cr);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Matches(int cb, int cr)
    {
        return (HasTeamA && TeamA.Matches(cb, cr)) ||
               (HasTeamB && TeamB.Matches(cb, cr));
    }
}

public readonly record struct BallDetectionColorModel(
    int CenterCb,
    int CenterCr,
    int RadiusSquared,
    int MinimumChromaticDistanceSquared)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Matches(int cb, int cr)
    {
        int neutralCb = cb - 128;
        int neutralCr = cr - 128;

        if (((neutralCb * neutralCb) + (neutralCr * neutralCr)) < MinimumChromaticDistanceSquared)
        {
            return false;
        }

        int dCb = cb - CenterCb;
        int dCr = cr - CenterCr;

        return ((dCb * dCb) + (dCr * dCr)) <= RadiusSquared;
    }
}
