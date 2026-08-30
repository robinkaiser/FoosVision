// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.TableConfig.Processing;

namespace FoosVision.Vision.UnitTests.TableConfig;

public class EquallySpacedPointsFinderTests
{
    [Fact]
    public void SelectSequences_Returns_Best_Sequence_When_Limit_Is_One()
    {
        List<double> points =
        [
             50,
            101, // !
            111,
            207, // !
            211,
            280,
            307, // !
            350,
            407, // !
            444,
            507, // !
            590,
            610, // !
            710, // !
            812, // !
            816,
            820,
        ];

        List<int> expectedIndices =
        [
            1,
            3,
            6,
            8,
            10,
            12,
            13,
            14,
        ];

        var pointPairs = points.Select(p => new PfPoint(p, p + 300)).ToArray();
        var sequences = EquallySpacedPointsFinder.SelectSequences(pointPairs, pointPairs.Length, 8, 1);

        var sequence = Assert.Single(sequences);
        Assert.True(expectedIndices.SequenceEqual(sequence.Indices));
    }

    [Fact]
    public void SelectSequences_Returns_Best_Sequences_Ordered_By_Error()
    {
        List<double> points =
        [
             50,
            101, // !
            111,
            207, // !
            211,
            280,
            307, // !
            350,
            407, // !
            444,
            507, // !
            590,
            610, // !
            710, // !
            812, // !
            816,
            820,
        ];

        List<int> expectedIndices =
        [
            1,
            3,
            6,
            8,
            10,
            12,
            13,
            14,
        ];

        var pointPairs = points.Select(p => new PfPoint(p, p + 300)).ToArray();
        var sequences = EquallySpacedPointsFinder.SelectSequences(pointPairs, pointPairs.Length, 8, 3);

        Assert.Equal(3, sequences.Count);
        Assert.True(expectedIndices.SequenceEqual(sequences[0].Indices));
        Assert.True(sequences[0].Error <= sequences[1].Error);
        Assert.True(sequences[1].Error <= sequences[2].Error);
    }

    [Fact]
    public void Returns_Empty_When_Points_Cannot_Satisfy_Minimum_Distance()
    {
        PfPoint[] points =
        [
            new(0, 300),
            new(99, 399),
            new(198, 498),
        ];

        var sequences = EquallySpacedPointsFinder.SelectSequences(points, points.Length, 3, 1);

        Assert.Empty(sequences);
    }
}
