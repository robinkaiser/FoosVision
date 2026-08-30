// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;
using FoosVision.Common.Logging;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Core.DecodedFrames;

public class FramePool : IFrameSink
{
    private static readonly Source _Log = new("FramePool");

    private readonly Lock _Lock = new();
    private readonly FrameLease[] _Frames;
    private readonly Stack<int> _FreeIndices;

    private ulong _NextFrameId;
    private Action<FrameLease>? _FrameReadyNotificationSink;

    public FramePool(int poolSize, FrameLayout layout)
    {
        _Frames = new FrameLease[poolSize];
        _FreeIndices = new Stack<int>(poolSize);

        for (int i = 0; i < poolSize; i++)
        {
            _Frames[i] = new FrameLease(this, i, layout);
            _FreeIndices.Push(i);
        }
    }

    // IFrameSink

    public IProducerFrameHandle AcquireForWrite()
    {
        FrameLease lease;

        lock (_Lock)
        {
            if (_FreeIndices.Count == 0)
            {
                _Log.Warning("AcquireForWrite - Out of buffers!");
                return NullProducerFrameHandle.Instance;
            }

            int idx = _FreeIndices.Pop();
            lease = _Frames[idx];
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

    public void OnFrameCommitted(FrameLease lease)
    {
        var handler = _FrameReadyNotificationSink;

        if (handler == null)
        {
            Recycle(lease);
            return;
        }

        lease.AddRefForConsumer();
        handler(lease);
    }

    public void Release(FrameLease lease)
    {
        int remaining = lease.DecrementRef();

        if (remaining <= 0)
        {
            Recycle(lease);
        }
    }

    // Controller interface

    public bool TryAcquireById(ulong id, [NotNullWhen(true)] out IFrameHandle? handle)
    {
        FrameLease? lease = _Frames.FirstOrDefault(f => f.IsCommitted && f.Meta.Id == id);

        if (lease == null)
        {
            handle = null;
            return false;
        }

        lease.AddRefForConsumer();
        handle = lease;
        return true;
    }

    public void SetFrameReadyNotificationSink(Action<FrameLease> sink)
    {
        _FrameReadyNotificationSink = sink;
    }

    private void Recycle(FrameLease lease)
    {
        lease.RecycleToFree();

        lock (_Lock)
        {
            _FreeIndices.Push(lease.PoolIndex);
        }
    }
}
