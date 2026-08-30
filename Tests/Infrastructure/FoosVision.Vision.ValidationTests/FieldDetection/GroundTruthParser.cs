// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Text.Json;
using System.Text.Json.Serialization;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;

namespace FoosVision.Vision.ValidationTests.FieldDetection;

public record TableBar
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BarType Type { get; init; }
    public TableBarLine Left { get; init; } = default!;
    public TableBarLine Right { get; init; } = default!;
}

public record TableBarLine
{
    public int P0X { get; init; }
    public int P0Y { get; init; }
    public int P1X { get; init; }
    public int P1Y { get; init; }
}

public record TablePoint
{
    public int X { get; init; }
    public int Y { get; init; }
}

public record TableOcclusion
{
    public TablePoint UpperLeft { get; init; } = default!;
    public TablePoint UpperRight { get; init; } = default!;
    public TablePoint LowerRight { get; init; } = default!;
    public TablePoint LowerLeft { get; init; } = default!;
}

public class TableConfig
{
    public IEnumerable<TableBar> Bars { get; init; } = default!;
    public IEnumerable<TableOcclusion> Occlusions { get; init; } = default!;
}

public static class GroundTruthParser
{
    private static readonly JsonSerializerOptions _Options;

    static GroundTruthParser()
    {
        _Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };
    }

    public static TableConfig Read(string fullPath)
    {
        var json = File.ReadAllText(fullPath);

        var config = JsonSerializer.Deserialize<TableConfig>(json, _Options)
            ?? throw new InvalidOperationException("Invalid JSON!");

        return config;
    }

    public static void WriteInitial(string fullPath, PlayingField field)
    {
        TableConfig config = new()
        {
            Bars = field.Bars.All.Select(ToTableBar).ToArray(),
            Occlusions = field.Occlusions.Select(ToTableOcclusion).ToArray(),
        };

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream stream = new(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, config, _Options);
    }

    private static TableBar ToTableBar(Bar bar)
    {
        return new TableBar
        {
            Type = bar.Type,
            Left = ToTableBarLine(bar.Left),
            Right = ToTableBarLine(bar.Right),
        };
    }

    private static TableBarLine ToTableBarLine(Line line)
    {
        return new TableBarLine
        {
            P0X = ToCoordinate(line.P0.X),
            P0Y = ToCoordinate(line.P0.Y),
            P1X = ToCoordinate(line.P1.X),
            P1Y = ToCoordinate(line.P1.Y),
        };
    }

    private static TableOcclusion ToTableOcclusion(Trapezium occlusion)
    {
        return new TableOcclusion
        {
            UpperLeft = ToTablePoint(occlusion.UpperLeft),
            UpperRight = ToTablePoint(occlusion.UpperRight),
            LowerRight = ToTablePoint(occlusion.LowerRight),
            LowerLeft = ToTablePoint(occlusion.LowerLeft),
        };
    }

    private static TablePoint ToTablePoint(Point point)
    {
        return new TablePoint
        {
            X = ToCoordinate(point.X),
            Y = ToCoordinate(point.Y),
        };
    }

    private static int ToCoordinate(double value)
    {
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
