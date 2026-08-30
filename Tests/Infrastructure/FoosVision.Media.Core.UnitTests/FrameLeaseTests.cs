// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.DecodedFrames;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Core.UnitTests;

public class FrameLeaseTests
{
    private const int _PoolIndex = 7;
    private const long _TimeStamp = 42;

    private readonly FramePool _Pool;
    private readonly FrameLease _Testee;

    public FrameLeaseTests()
    {
        FrameLayout layout = new(FrameByteFormat.RGBA8888, 8, 8, 16);
        _Pool = new FramePool(1, layout);
        _Pool.SetFrameReadyNotificationSink((lease) => { });
        _Testee = new FrameLease(_Pool, _PoolIndex, layout);
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

        _Testee.MarkWritten(_TimeStamp);
        Assert.Equal(_TimeStamp, _Testee.Meta.TimestampNs);
        Assert.False(_Testee.IsFree); // Ref = 1

        Assert.Equal(0, _Testee.DecrementRef());

        _Testee.RecycleToFree();
        Assert.True(_Testee.IsFree);
    }

    [Fact]
    public void Two_consumer_lifecycle()
    {
        _Testee.BeginWrite();
        _Testee.MarkWritten(_TimeStamp);

        _Testee.AddRefForConsumer();
        Assert.Equal(1, _Testee.DecrementRef());
    }
}
