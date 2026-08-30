// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.Table.Services;

public static class TableBarClassifier
{
    public static Team GetTeam(BarType barType)
        => barType switch
        {
            BarType.A1 or
            BarType.A2 or
            BarType.A5 or
            BarType.A3 => Team.A,
            BarType.B3 or
            BarType.B5 or
            BarType.B2 or
            BarType.B1 => Team.B,
            _ => Team.None,
        };

    public static PossessionArea GetPossessionArea(BarType barType)
        => barType switch
        {
            BarType.A1 or
            BarType.A2 or
            BarType.B1 or
            BarType.B2 => PossessionArea.Defense,
            BarType.A5 or
            BarType.B5 => PossessionArea.FiveBar,
            BarType.A3 or
            BarType.B3 => PossessionArea.ThreeBar,
            _ => PossessionArea.None,
        };

    public static bool IsDefenseBar(BarType barType)
        => barType is
            BarType.A1 or
            BarType.A2 or
            BarType.B1 or
            BarType.B2;

    public static bool IsThreeBar(BarType barType)
        => barType is
            BarType.A3 or
            BarType.B3;
}
