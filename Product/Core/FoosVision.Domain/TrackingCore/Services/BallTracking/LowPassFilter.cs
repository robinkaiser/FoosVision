// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Domain.TrackingCore.Services.BallTracking;

public class LowPassFilter
{
    private bool _FirstTime;
    private double _S;

    public LowPassFilter(LowPassFilter other)
    {
        _FirstTime = other._FirstTime;
        _S = other._S;
        Alpha = other.Alpha;
    }

    public LowPassFilter()
    {
        _FirstTime = true;
        Alpha = 0.5;
    }

    public LowPassFilter(double alpha)
    {
        _FirstTime = true;
        Alpha = alpha;
    }

    public double Last => _S;

    public double Alpha { get; set; }

    public void Reset()
    {
        _FirstTime = true;
    }

    public void Reset(double x)
    {
        _FirstTime = false;
        _S = x;
    }

    public double Filter(double x)
    {
        return Filter(x, Alpha);
    }

    public double Filter(double x, double alpha)
    {
        if (_FirstTime)
        {
            Reset(x);
            return _S;
        }

        _S = (alpha * x) + ((1.0 - alpha) * _S);

        return _S;
    }
}
