// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Protocol.Messages.Live;

namespace FoosVision.Adapters.Common.Live;

public static class TableConfigurationMessageMapper
{
    public static TableConfigurationMessage Map(TableConfiguration config)
    {
        return new TableConfigurationMessage
        {
            Boundary = CreateTrapezium(config.Field.Boundary),
            Bars = [.. config.Field.Bars.All.Select(CreateBar)],
            Occlusions = [.. config.Field.Occlusions.Select(CreateTrapezium)],
            TeamAPlayerColorArgb = config.Players.TeamAArgb,
            TeamBPlayerColorArgb = config.Players.TeamBArgb,
        };
    }

    private static BarMessage CreateBar(Bar bar)
        => new()
        {
            Type = CreateBarType(bar.Type),
            Left = CreateLine(bar.Left),
            Center = CreateLine(bar.Center),
            Right = CreateLine(bar.Right),
        };

    private static TrapeziumMessage CreateTrapezium(Trapezium trapezium)
        => new()
        {
            UpperLeft = CreatePoint(trapezium.UpperLeft),
            UpperRight = CreatePoint(trapezium.UpperRight),
            LowerLeft = CreatePoint(trapezium.LowerLeft),
            LowerRight = CreatePoint(trapezium.LowerRight),
        };

    private static LineMessage CreateLine(Line line)
        => new()
        {
            P0 = CreatePoint(line.P0),
            P1 = CreatePoint(line.P1),
        };

    private static PointMessage CreatePoint(Point point)
        => new()
        {
            X = point.X,
            Y = point.Y,
        };

    private static BarTypeMessage CreateBarType(BarType type)
        => type switch
        {
            BarType.A1 => BarTypeMessage.A1,
            BarType.A2 => BarTypeMessage.A2,
            BarType.B3 => BarTypeMessage.B3,
            BarType.A5 => BarTypeMessage.A5,
            BarType.B5 => BarTypeMessage.B5,
            BarType.A3 => BarTypeMessage.A3,
            BarType.B2 => BarTypeMessage.B2,
            BarType.B1 => BarTypeMessage.B1,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
}
