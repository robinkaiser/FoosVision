// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Core.DecodedFrames;

public class FrameLease : IProducerFrameHandle, IFrameHandle
{
    private enum State
    {
        Free,
        BeingWritten,
        Committed,
    }

    private readonly FramePool _Pool;

    // 0 = free in pool
    // >0 = number of active consumers holding this frame
    private int _RefCount;
    private State _State;

    public FrameLease(FramePool pool, int poolIndex, FrameLayout layout)
    {
        _Pool = pool;
        PoolIndex = poolIndex;

        var capacityBytes = layout.Stride * layout.Height;
        BufferRGBA8888 = new byte[capacityBytes];
        Layout = layout;

        _RefCount = 0;
        _State = State.Free;
    }

    // IProducerFrameHandle

    public byte[] BufferRGBA8888 { get; private set; }

    public void MarkWritten(long timestampNs)
    {
        var id = _Pool.GetNextId();
        Meta = new(id, timestampNs);
        _State = State.Committed;
        _Pool.OnFrameCommitted(this);
    }

    // IFrameHandle

    public Frame Meta { get; private set; }

    public FrameLayout Layout { get; private set; }

    public void Release()
    {
        _Pool.Release(this);
    }

    // Pool interface

    public int PoolIndex { get; }

    public void BeginWrite()
    {
        Meta = new();
        _State = State.BeingWritten;
        _RefCount = 0; // Producer is not counted as a consumer
    }

    public void AddRefForConsumer()
    {
        Interlocked.Increment(ref _RefCount);
    }

    public int DecrementRef()
    {
        return Interlocked.Decrement(ref _RefCount);
    }

    public bool IsFree => _State == State.Free;

    public bool IsCommitted => _State == State.Committed;

    public void RecycleToFree()
    {
        _State = State.Free;
    }
}
