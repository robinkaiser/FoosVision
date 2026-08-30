// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using MessagePack;

namespace FoosVision.Protocol.Messages.Live;

public enum BarTypeMessage
{
    A1,
    A2,
    B3,
    A5,
    B5,
    A3,
    B2,
    B1,
}

[MessagePackObject(true)]
public record BarMessage
{
    public BarTypeMessage Type { get; init; }

    public LineMessage Left { get; init; } = new();

    public LineMessage Center { get; init; } = new();

    public LineMessage Right { get; init; } = new();
}

[MessagePackObject(true)]
public record TrapeziumMessage
{
    public PointMessage UpperLeft { get; init; } = new();

    public PointMessage UpperRight { get; init; } = new();

    public PointMessage LowerLeft { get; init; } = new();

    public PointMessage LowerRight { get; init; } = new();
}

[MessagePackObject(true)]
public record LineMessage
{
    public PointMessage P0 { get; init; } = new();

    public PointMessage P1 { get; init; } = new();
}

[MessagePackObject(true)]
public record PointMessage
{
    public double X { get; init; }

    public double Y { get; init; }
}

[MessagePackObject(true)]
public record VectorMessage
{
    public double X { get; init; }

    public double Y { get; init; }
}
