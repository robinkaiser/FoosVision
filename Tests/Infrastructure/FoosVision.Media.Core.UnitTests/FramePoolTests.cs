// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.DecodedFrames;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Core.UnitTests;

public class FramePoolTests
{
    private const int _PoolSize = 2;
    private const long _TimestampNs = 42;

    private readonly FrameLayout _Layout;
    private readonly FramePool _Testee;
    private readonly List<FrameLease> _Received;

    public FramePoolTests()
    {
        _Layout = new FrameLayout(FrameByteFormat.RGBA8888, 16, 32, 16);
        _Testee = new FramePool(_PoolSize, _Layout);
        _Received = [];
        _Testee.SetFrameReadyNotificationSink(lease => _Received.Add(lease));
    }

    [Fact]
    public void Fixture()
    {
    }

    [Fact]
    public void Acquire_for_write_and_commit()
    {
        var producerHandle = _Testee.AcquireForWrite();
        var lease = Assert.IsType<FrameLease>(producerHandle);
        Assert.Equal(_Layout.Stride * _Layout.Height, lease.BufferRGBA8888.Length);

        lease.MarkWritten(_TimestampNs);
        Assert.Same(lease, _Received.Single());
    }

    [Fact]
    public void Acquire_leads_to_buffer_overflow()
    {
        _Testee.AcquireForWrite();
        _Testee.AcquireForWrite();
        var producerHandle = _Testee.AcquireForWrite();

        Assert.IsType<NullProducerFrameHandle>(producerHandle);
    }

    [Fact]
    public void Release_frame()
    {
        var lease = (FrameLease)_Testee.AcquireForWrite();
        lease.MarkWritten(_TimestampNs);
        lease.Release();

        Assert.True(lease.IsFree);

        _Testee.AcquireForWrite();
        var producerHandle = _Testee.AcquireForWrite();
        Assert.IsNotType<NullProducerFrameHandle>(producerHandle);
    }

    [Fact]
    public void Try_aquire_frame_by_id()
    {
        var lease = (FrameLease)_Testee.AcquireForWrite();
        Assert.False(_Testee.TryAcquireById(0, out _));

        lease.MarkWritten(_TimestampNs);
        Assert.True(_Testee.TryAcquireById(0, out var frame));
        Assert.Equal(_TimestampNs, frame.Meta.TimestampNs);

        frame.Release();
        Assert.True(_Testee.TryAcquireById(0, out _));

        frame.Release();
        frame.Release();
        Assert.False(_Testee.TryAcquireById(0, out _));
    }
}
