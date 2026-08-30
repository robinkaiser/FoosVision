// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Globalization;
using System.Text;

namespace FoosVision.Common.Metrics;

internal class MetricSampleAccumulator
{
    private static readonly double[] _DefaultBucketUpperBoundsMs =
    [
        0.25D,
        0.5D,
        1D,
        2D,
        4D,
        8D,
        16D,
        33.333D,
        50D,
        66.667D,
        100D,
        200D,
        500D,
        1000D,
    ];

    private readonly double[] _BucketUpperBoundsMs;
    private readonly long[] _BucketCounts;

    private long _Count;
    private double _TotalMs;
    private double _MinMs = double.PositiveInfinity;
    private double _MaxMs;

    public MetricSampleAccumulator(IReadOnlyList<double>? bucketUpperBoundsMs)
    {
        _BucketUpperBoundsMs = bucketUpperBoundsMs?.ToArray() ?? _DefaultBucketUpperBoundsMs;
        ValidateBuckets(_BucketUpperBoundsMs);
        _BucketCounts = new long[_BucketUpperBoundsMs.Length + 1];
    }

    public void Record(double valueMs)
    {
        _Count++;
        _TotalMs += valueMs;
        _MinMs = Math.Min(_MinMs, valueMs);
        _MaxMs = Math.Max(_MaxMs, valueMs);

        int bucketIndex = _BucketUpperBoundsMs.Length;

        for (int i = 0; i < _BucketUpperBoundsMs.Length; i++)
        {
            if (valueMs <= _BucketUpperBoundsMs[i])
            {
                bucketIndex = i;
                break;
            }
        }

        _BucketCounts[bucketIndex]++;
    }

    public MetricSampleSnapshot SnapshotAndReset()
    {
        MetricSampleSnapshot snapshot = _Count == 0
            ? new MetricSampleSnapshot(0, 0, 0, 0, FormatBuckets())
            : new MetricSampleSnapshot(_Count, _TotalMs / _Count, _MinMs, _MaxMs, FormatBuckets());

        Array.Clear(_BucketCounts);
        _Count = 0;
        _TotalMs = 0;
        _MinMs = double.PositiveInfinity;
        _MaxMs = 0;

        return snapshot;
    }

    private string FormatBuckets()
    {
        StringBuilder builder = new();

        for (int i = 0; i < _BucketUpperBoundsMs.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder
                .Append("<=")
                .Append(_BucketUpperBoundsMs[i].ToString("0.###", CultureInfo.InvariantCulture))
                .Append("ms:")
                .Append(_BucketCounts[i].ToString(CultureInfo.InvariantCulture));
        }

        builder
            .Append(", >")
            .Append(_BucketUpperBoundsMs[^1].ToString("0.###", CultureInfo.InvariantCulture))
            .Append("ms:")
            .Append(_BucketCounts[^1].ToString(CultureInfo.InvariantCulture));

        return builder.ToString();
    }

    private static void ValidateBuckets(double[] bucketUpperBoundsMs)
    {
        if (bucketUpperBoundsMs.Length == 0)
        {
            throw new ArgumentException("At least one bucket upper bound is required.", nameof(bucketUpperBoundsMs));
        }

        double previous = 0;

        foreach (double upperBoundMs in bucketUpperBoundsMs)
        {
            if (upperBoundMs <= previous)
            {
                throw new ArgumentException("Bucket upper bounds must be positive and strictly ascending.", nameof(bucketUpperBoundsMs));
            }

            previous = upperBoundMs;
        }
    }
}
