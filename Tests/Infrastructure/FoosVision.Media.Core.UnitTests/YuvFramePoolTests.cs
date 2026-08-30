// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.DecodedYuvFrames;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Core.UnitTests;

public class YuvFramePoolTests
{
    private const int _PoolSize = 2;
    private const long _TimestampNs = 42;

    private readonly YuvFrameLayout _Layout;
    private readonly YuvFramePool _Testee;
    private readonly List<YuvFrameLease> _Received;

    public YuvFramePoolTests()
    {
        _Layout = CreateLayout();
        _Testee = new YuvFramePool(_PoolSize, _Layout);
        _Received = [];
        _Testee.SetFrameReadyNotificationSink(lease => _Received.Add(lease));
    }

    [Fact]
    public void Acquire_for_write_and_commit()
    {
        IProducerYuvFrameHandle producerHandle = _Testee.AcquireForWrite();
        YuvFrameLease lease = Assert.IsType<YuvFrameLease>(producerHandle);

        Assert.Equal(_Layout.Y.BufferLength, lease.BufferY.Length);
        Assert.Equal(_Layout.U.BufferLength, lease.BufferU.Length);
        Assert.Equal(_Layout.V.BufferLength, lease.BufferV.Length);

        lease.MarkWritten(_TimestampNs);

        Assert.Same(lease, _Received.Single());
    }

    [Fact]
    public void Acquire_leads_to_buffer_overflow()
    {
        _Testee.AcquireForWrite();
        _Testee.AcquireForWrite();

        IProducerYuvFrameHandle producerHandle = _Testee.AcquireForWrite();

        Assert.IsType<NullProducerYuvFrameHandle>(producerHandle);
    }

    [Fact]
    public void Release_frame()
    {
        YuvFrameLease lease = (YuvFrameLease)_Testee.AcquireForWrite();
        lease.MarkWritten(_TimestampNs);
        lease.Release();

        Assert.True(lease.IsFree);

        _Testee.AcquireForWrite();
        IProducerYuvFrameHandle producerHandle = _Testee.AcquireForWrite();
        Assert.IsNotType<NullProducerYuvFrameHandle>(producerHandle);
    }

    [Fact]
    public void Try_acquire_frame_by_id()
    {
        YuvFrameLease lease = (YuvFrameLease)_Testee.AcquireForWrite();
        Assert.False(_Testee.TryAcquireById(0, out _));

        lease.MarkWritten(_TimestampNs);
        Assert.True(_Testee.TryAcquireById(0, out IYuvFrameHandle? frame));
        Assert.Equal(_TimestampNs, frame.Meta.TimestampNs);

        frame.Release();
        Assert.True(_Testee.TryAcquireById(0, out _));

        frame.Release();
        frame.Release();
        Assert.False(_Testee.TryAcquireById(0, out _));
    }

    private static YuvFrameLayout CreateLayout()
    {
        YuvPlaneLayout y = new(YuvPlaneKind.Y, 16, 16, 16, 1);
        YuvPlaneLayout u = new(YuvPlaneKind.U, 8, 8, 8, 1);
        YuvPlaneLayout v = new(YuvPlaneKind.V, 8, 8, 8, 1);
        return new YuvFrameLayout(16, 16, y, u, v);
    }
}
