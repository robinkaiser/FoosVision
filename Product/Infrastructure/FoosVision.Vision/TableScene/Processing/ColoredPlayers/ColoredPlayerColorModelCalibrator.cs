// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.Services;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Vision.TableScene.Processing.Common;

namespace FoosVision.Vision.TableScene.Processing.ColoredPlayers;

public class ColoredPlayerColorModelCalibrator
{
    private readonly ColoredPlayerColorModelCalibrationOptions _Options;
    private readonly int[] _CbSamples;
    private readonly int[] _CrSamples;
    private readonly int[] _SortedCb;
    private readonly int[] _SortedCr;
    private readonly double[] _Distances;

    public ColoredPlayerColorModelCalibrator(
        ColoredPlayerColorModelCalibrationOptions? options = null,
        int sampleCapacity = 4096)
    {
        _Options = options ?? new ColoredPlayerColorModelCalibrationOptions();
        int capacity = Math.Max(1, sampleCapacity);
        _CbSamples = new int[capacity];
        _CrSamples = new int[capacity];
        _SortedCb = new int[capacity];
        _SortedCr = new int[capacity];
        _Distances = new double[capacity];
    }

    public ColoredPlayerColorCalibration Calibrate(ColoredRodObjectIntervalDetection detection)
    {
        TeamColorCalibration teamA = CalibrateTeam(detection, Team.A);
        TeamColorCalibration teamB = CalibrateTeam(detection, Team.B);

        return new(teamA, teamB);
    }

    private TeamColorCalibration CalibrateTeam(ColoredRodObjectIntervalDetection detection, Team team)
    {
        int intervalCount = CountIntervals(detection, team);

        if (intervalCount < _Options.MinimumIntervalsPerTeam)
        {
            return new(team, intervalCount, 0, null);
        }

        int capacity = CountSamples(detection, team);
        ValidateSampleCapacity(capacity);
        int chromaticSampleCount = CollectChromaticSamples(detection, team);

        if (chromaticSampleCount == 0)
        {
            return new(team, intervalCount, 0, null);
        }

        ChromaticColorModel model = CreateModel(chromaticSampleCount);

        return new(team, intervalCount, chromaticSampleCount, model);
    }

    private static int CountIntervals(ColoredRodObjectIntervalDetection detection, Team team)
    {
        int count = 0;

        for (int i = 0; i < detection.Rods.Count; i++)
        {
            var rod = detection.Rods[i];

            if (TableBarClassifier.GetTeam(rod.BarType) != team)
            {
                continue;
            }

            count += rod.Intervals.Count;
        }

        return count;
    }

    private static int CountSamples(ColoredRodObjectIntervalDetection detection, Team team)
    {
        int count = 0;

        for (int i = 0; i < detection.Rods.Count; i++)
        {
            var rod = detection.Rods[i];

            if (TableBarClassifier.GetTeam(rod.BarType) != team)
            {
                continue;
            }

            for (int j = 0; j < rod.Intervals.Count; j++)
            {
                var interval = rod.Intervals[j];

                if (rod.SampleProfile.Count == 0)
                {
                    continue;
                }

                int startIndex = Math.Clamp(interval.StartIndex, 0, rod.SampleProfile.Count - 1);
                int endIndex = Math.Clamp(interval.EndIndex, 0, rod.SampleProfile.Count - 1);
                count += Math.Max(0, endIndex - startIndex + 1);
            }
        }

        return count;
    }

    private int CollectChromaticSamples(
        ColoredRodObjectIntervalDetection detection,
        Team team)
    {
        int count = 0;

        for (int rodIndex = 0; rodIndex < detection.Rods.Count; rodIndex++)
        {
            var rod = detection.Rods[rodIndex];

            if (TableBarClassifier.GetTeam(rod.BarType) != team)
            {
                continue;
            }

            for (int intervalIndex = 0; intervalIndex < rod.Intervals.Count; intervalIndex++)
            {
                var interval = rod.Intervals[intervalIndex];

                if (rod.SampleProfile.Count == 0)
                {
                    continue;
                }

                int startIndex = Math.Clamp(interval.StartIndex, 0, rod.SampleProfile.Count - 1);
                int endIndex = Math.Clamp(interval.EndIndex, 0, rod.SampleProfile.Count - 1);

                for (int i = startIndex; i <= endIndex; i++)
                {
                    if (rod.SampleProfile.Occluded[i])
                    {
                        continue;
                    }

                    ColorFeature feature = rod.SampleProfile.Features[i];

                    if (!IsChromatic(feature))
                    {
                        continue;
                    }

                    _CbSamples[count] = feature.Cb;
                    _CrSamples[count] = feature.Cr;
                    count++;
                }
            }
        }

        return count;
    }

    private ChromaticColorModel CreateModel(int count)
    {
        Array.Copy(_CbSamples, _SortedCb, count);
        Array.Copy(_CrSamples, _SortedCr, count);
        Array.Sort(_SortedCb, 0, count);
        Array.Sort(_SortedCr, 0, count);

        int centerCb = _SortedCb[count / 2];
        int centerCr = _SortedCr[count / 2];

        for (int i = 0; i < count; i++)
        {
            int dCb = _CbSamples[i] - centerCb;
            int dCr = _CrSamples[i] - centerCr;
            _Distances[i] = Math.Sqrt((dCb * dCb) + (dCr * dCr));
        }

        Array.Sort(_Distances, 0, count);

        int percentileIndex = Math.Clamp(
            Convert.ToInt32(Math.Ceiling((count - 1) * _Options.RadiusPercentile)),
            0,
            count - 1);
        double radius = Math.Clamp(
            _Distances[percentileIndex] + _Options.RadiusMargin,
            _Options.MinimumRadius,
            _Options.MaximumRadius);

        return new(
            centerCb,
            centerCr,
            radius,
            _Options.MinimumChromaticDistance,
            count);
    }

    private bool IsChromatic(ColorFeature feature)
    {
        int dCb = feature.Cb - 128;
        int dCr = feature.Cr - 128;
        int chromaticDistanceSquared = (dCb * dCb) + (dCr * dCr);
        int minimumDistanceSquared = _Options.MinimumChromaticDistance * _Options.MinimumChromaticDistance;

        return chromaticDistanceSquared >= minimumDistanceSquared;
    }

    private void ValidateSampleCapacity(int capacity)
    {
        if (_CbSamples.Length >= capacity)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Colored player color calibration needs {capacity} samples, but the calibrator was constructed for {_CbSamples.Length} samples.");
    }
}

public record ColoredPlayerColorModelCalibrationOptions(
    int MinimumIntervalsPerTeam = 5,
    int MinimumChromaticDistance = 25,
    double RadiusPercentile = 0.85,
    double RadiusMargin = 12,
    double MinimumRadius = 20,
    double MaximumRadius = 80);
