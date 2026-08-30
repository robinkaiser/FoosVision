// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Protocol.Messages.Live;

namespace FoosVision.Adapters.Viewer.Session;

internal static class TableConfigurationMessageMapper
{
    public static bool TryMap(TableConfigurationMessage message, out TableConfiguration tableConfiguration)
    {
        Dictionary<BarType, Bar> bars = [];

        foreach (BarMessage bar in message.Bars)
        {
            Bar mapped = CreateBar(bar);
            bars[mapped.Type] = mapped;
        }

        if (!TryGetBars(bars, out TableBars tableBars))
        {
            tableConfiguration = default!;
            return false;
        }

        PlayingField field = new(
            Boundary: CreateTrapezium(message.Boundary),
            Bars: tableBars,
            Occlusions: [.. message.Occlusions.Select(CreateTrapezium)]);

        tableConfiguration = new(
            Field: field,
            Players: new(message.TeamAPlayerColorArgb, message.TeamBPlayerColorArgb),
            Ball: BallColor.Unknown);
        return true;
    }

    private static bool TryGetBars(IReadOnlyDictionary<BarType, Bar> bars, out TableBars tableBars)
    {
        if (!bars.TryGetValue(BarType.A1, out Bar? a1) ||
            !bars.TryGetValue(BarType.A2, out Bar? a2) ||
            !bars.TryGetValue(BarType.B3, out Bar? b3) ||
            !bars.TryGetValue(BarType.A5, out Bar? a5) ||
            !bars.TryGetValue(BarType.B5, out Bar? b5) ||
            !bars.TryGetValue(BarType.A3, out Bar? a3) ||
            !bars.TryGetValue(BarType.B2, out Bar? b2) ||
            !bars.TryGetValue(BarType.B1, out Bar? b1))
        {
            tableBars = default!;
            return false;
        }

        tableBars = new(a1, a2, b3, a5, b5, a3, b2, b1);
        return true;
    }

    private static Bar CreateBar(BarMessage message)
    {
        BarType type = message.Type switch
        {
            BarTypeMessage.A1 => BarType.A1,
            BarTypeMessage.A2 => BarType.A2,
            BarTypeMessage.B3 => BarType.B3,
            BarTypeMessage.A5 => BarType.A5,
            BarTypeMessage.B5 => BarType.B5,
            BarTypeMessage.A3 => BarType.A3,
            BarTypeMessage.B2 => BarType.B2,
            BarTypeMessage.B1 => BarType.B1,
            _ => throw new ArgumentOutOfRangeException(nameof(message)),
        };

        return new(
            type,
            CreateLine(message.Left),
            CreateLine(message.Center),
            CreateLine(message.Right));
    }

    private static Trapezium CreateTrapezium(TrapeziumMessage message)
    {
        return new(
            UpperLeft: CreatePoint(message.UpperLeft),
            UpperRight: CreatePoint(message.UpperRight),
            LowerLeft: CreatePoint(message.LowerLeft),
            LowerRight: CreatePoint(message.LowerRight));
    }

    private static Line CreateLine(LineMessage message)
    {
        return new(
            P0: CreatePoint(message.P0),
            P1: CreatePoint(message.P1));
    }

    private static Point CreatePoint(PointMessage message)
    {
        return new(message.X, message.Y);
    }
}
