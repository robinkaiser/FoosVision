// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Common.Metrics;

internal readonly record struct MetricSampleSnapshot(
    long Count,
    double AverageMs,
    double MinMs,
    double MaxMs,
    string Buckets);
