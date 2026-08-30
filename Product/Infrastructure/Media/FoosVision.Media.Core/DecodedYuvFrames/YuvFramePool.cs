// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;
using FoosVision.Common.Logging;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Core.DecodedYuvFrames;

public class YuvFramePool : IYuvFrameSink
{
    private static readonly Source _Log = new("YuvFramePool");

    private readonly Lock _Lock = new();
    private readonly YuvFrameLease[] _Frames;
    private readonly Stack<int> _FreeIndices;

    private ulong _NextFrameId;
    private Action<YuvFrameLease>? _FrameReadyNotificationSink;

    public YuvFramePool(int poolSize, YuvFrameLayout layout)
    {
        Layout = layout;
        _Frames = new YuvFrameLease[poolSize];
        _FreeIndices = new Stack<int>(poolSize);

        for (int i = 0; i < poolSize; i++)
        {
            _Frames[i] = new YuvFrameLease(this, i, layout);
            _FreeIndices.Push(i);
        }
    }

    public YuvFrameLayout Layout { get; }

    // IYuvFrameSink

    public IProducerYuvFrameHandle AcquireForWrite()
    {
        YuvFrameLease lease;

        lock (_Lock)
        {
            if (_FreeIndices.Count == 0)
            {
                _Log.Warning("AcquireForWrite - Out of buffers!");
                return NullProducerYuvFrameHandle.Instance;
            }

            int index = _FreeIndices.Pop();
            lease = _Frames[index];
        }

        lease.BeginWrite();
        return lease;
    }

    // FrameLease interface

    public ulong GetNextId()
    {
        ulong id = _NextFrameId;
        _NextFrameId++;
        return id;
    }

    public void OnFrameCommitted(YuvFrameLease lease)
    {
        Action<YuvFrameLease>? handler = _FrameReadyNotificationSink;

        if (handler == null)
        {
            Recycle(lease);
            return;
        }

        lease.AddRefForConsumer();
        handler(lease);
    }

    public void Release(YuvFrameLease lease)
    {
        int remaining = lease.DecrementRef();

        if (remaining <= 0)
        {
            Recycle(lease);
        }
    }

    // Controller interface

    public bool TryAcquireById(ulong id, [NotNullWhen(true)] out IYuvFrameHandle? handle)
    {
        YuvFrameLease? lease = _Frames.FirstOrDefault(f => f.IsCommitted && f.Meta.Id == id);

        if (lease == null)
        {
            handle = null;
            return false;
        }

        lease.AddRefForConsumer();
        handle = lease;
        return true;
    }

    public void SetFrameReadyNotificationSink(Action<YuvFrameLease> sink)
    {
        _FrameReadyNotificationSink = sink;
    }

    private void Recycle(YuvFrameLease lease)
    {
        lease.RecycleToFree();

        lock (_Lock)
        {
            _FreeIndices.Push(lease.PoolIndex);
        }
    }
}
