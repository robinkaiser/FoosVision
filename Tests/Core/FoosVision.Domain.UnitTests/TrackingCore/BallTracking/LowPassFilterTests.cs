// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.TrackingCore.Services.BallTracking;

namespace FoosVision.Domain.UnitTests.TrackingCore.BallTracking;

public class LowPassFilterTests
{
    private readonly LowPassFilter _Testee;

    public LowPassFilterTests()
    {
        _Testee = new();
    }

    [Fact]
    public void Reset()
    {
        Assert.Equal(1.0, _Testee.Filter(1.0, 0.1));

        _Testee.Reset();

        Assert.Equal(2.0, _Testee.Filter(2.0, 0.1));
    }

    [Fact]
    public void Reset_value()
    {
        _Testee.Reset(10);

        Assert.Equal(15.0, _Testee.Filter(20, 0.5));
    }

    [Fact]
    public void Alpha_via_parameter()
    {
        Assert.Equal(1.0, _Testee.Filter(1.0, 0.1));
        Assert.Equal(1.0, _Testee.Last);

        // s(t) = alpha * x(t) + (1 - alpha) * s(t-1)
        Assert.Equal(1.1, _Testee.Filter(2.0, 0.1));
        Assert.Equal(2.81, _Testee.Filter(3.0, 0.9));
        Assert.Equal(2.81, _Testee.Last);
    }

    [Fact]
    public void Alpha_via_setter()
    {
        Assert.Equal(1.0, _Testee.Filter(1.0, 0.1));

        _Testee.Alpha = 0.1;
        Assert.Equal(1.1, _Testee.Filter(2.0));

        _Testee.Alpha = 0.9;
        Assert.Equal(2.81, _Testee.Filter(3.0));
        Assert.Equal(2.81, _Testee.Last);
    }

    [Fact]
    public void Alpha_via_ctor()
    {
        LowPassFilter testee = new(0.1);

        Assert.Equal(1.0, testee.Filter(1.0));
        Assert.Equal(1.1, testee.Filter(2.0));
    }

    [Fact]
    public void Copy_filter()
    {
        _Testee.Alpha = 0.1;
        _Testee.Filter(0.2);

        var copy = new LowPassFilter(_Testee);

        var f1 = _Testee.Filter(0.3);
        var f2 = copy.Filter(0.3);

        Assert.Equal(f1, f2);
    }
}
