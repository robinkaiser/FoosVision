// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Core.DecodedYuvFrames;

public class YuvFrameLease : IProducerYuvFrameHandle, IYuvFrameHandle
{
    private enum State
    {
        Free,
        BeingWritten,
        Committed,
    }

    private readonly YuvFramePool _Pool;

    // 0 = free in pool
    // >0 = number of active consumers holding this frame
    private int _RefCount;
    private State _State;

    public YuvFrameLease(YuvFramePool pool, int poolIndex, YuvFrameLayout layout)
    {
        _Pool = pool;
        PoolIndex = poolIndex;
        Layout = layout;

        BufferY = new byte[layout.Y.BufferLength];
        BufferU = new byte[layout.U.BufferLength];
        BufferV = new byte[layout.V.BufferLength];

        _RefCount = 0;
        _State = State.Free;
    }

    // IProducerYuvFrameHandle

    public byte[] BufferY { get; }

    public byte[] BufferU { get; }

    public byte[] BufferV { get; }

    public void MarkWritten(long timestampNs)
    {
        ulong id = _Pool.GetNextId();
        Meta = new(id, timestampNs);
        _State = State.Committed;
        _Pool.OnFrameCommitted(this);
    }

    // IYuvFrameHandle

    public Frame Meta { get; private set; }

    public YuvFrameLayout Layout { get; }

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
