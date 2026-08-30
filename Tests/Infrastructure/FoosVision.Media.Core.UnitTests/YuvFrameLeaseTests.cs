// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.DecodedYuvFrames;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Core.UnitTests;

public class YuvFrameLeaseTests
{
    private const int _PoolIndex = 7;
    private const long _TimestampNs = 42;

    private readonly YuvFramePool _Pool;
    private readonly YuvFrameLease _Testee;

    public YuvFrameLeaseTests()
    {
        YuvFrameLayout layout = CreateLayout();
        _Pool = new YuvFramePool(1, layout);
        _Pool.SetFrameReadyNotificationSink((lease) => { });
        _Testee = new YuvFrameLease(_Pool, _PoolIndex, layout);
    }

    [Fact]
    public void Fixture()
    {
        Assert.Equal(_PoolIndex, _Testee.PoolIndex);
        Assert.True(_Testee.IsFree);
    }

    [Fact]
    public void Single_consumer_lifecycle()
    {
        _Testee.BeginWrite();
        Assert.Equal(0, _Testee.Meta.TimestampNs);
        Assert.False(_Testee.IsFree);

        _Testee.MarkWritten(_TimestampNs);
        Assert.Equal(_TimestampNs, _Testee.Meta.TimestampNs);
        Assert.False(_Testee.IsFree); // Ref = 1

        Assert.Equal(0, _Testee.DecrementRef());

        _Testee.RecycleToFree();
        Assert.True(_Testee.IsFree);
    }

    [Fact]
    public void Two_consumer_lifecycle()
    {
        _Testee.BeginWrite();
        _Testee.MarkWritten(_TimestampNs);

        _Testee.AddRefForConsumer();

        Assert.Equal(1, _Testee.DecrementRef());
    }

    private static YuvFrameLayout CreateLayout()
    {
        YuvPlaneLayout y = new(YuvPlaneKind.Y, 16, 16, 16, 1);
        YuvPlaneLayout u = new(YuvPlaneKind.U, 8, 8, 8, 1);
        YuvPlaneLayout v = new(YuvPlaneKind.V, 8, 8, 8, 1);
        return new YuvFrameLayout(16, 16, y, u, v);
    }
}
