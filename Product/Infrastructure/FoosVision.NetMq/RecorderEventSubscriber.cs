// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Collections.Concurrent;
using FoosVision.NetMq.Internal;
using FoosVision.Protocol;
using FoosVision.Protocol.Connectivity.Abstractions;
using MessagePack;
using NetMQ.Sockets;

namespace FoosVision.NetMq;

public class RecorderEventSubscriber : IRecorderEventSubscriber
{
    private readonly SubscriberSocket _Socket = new();
    private readonly ConcurrentDictionary<EventMessageType, ConcurrentBag<Delegate>> _Handlers = new();

    private readonly CancellationTokenSource _Cts = new();
    private readonly TimeSpan _PollTimeout = TimeSpan.FromMilliseconds(250);
    private readonly Task _LoopTask;

    public RecorderEventSubscriber(string recorderEventsAddress)
    {
        _Socket.Connect(recorderEventsAddress);

        // Subscribe to all topics (empty prefix)
        _Socket.Subscribe([]);

        _LoopTask = Task.Run(() => Loop(_Cts.Token));
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> onMessage)
    {
        var type = ProtocolTypeRegistry.GetEventType<TEvent>();
        var bag = _Handlers.GetOrAdd(type, _ => []);
        bag.Add(onMessage);

        return new Subscription(() => { /* no removal for now (acceptable for RC); add later if needed */ });
    }

    public void Dispose()
    {
        _Cts.Cancel();

        try
        {
            _LoopTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
        }

        _Socket.Dispose();
        _Cts.Dispose();
    }

    private void Loop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // SUB sockets receive multipart too; reuse the same two-frame convention.
            if (!Frames.TryReceive(out var typeByte, out var payload, _Socket, _PollTimeout))
                continue;

            var type = (EventMessageType)typeByte;

            if (!_Handlers.TryGetValue(type, out var handlers) || handlers.IsEmpty)
                continue;

            var clrType = ProtocolTypeRegistry.GetEventClrType(type);
            object? evtObj;

            try
            {
                evtObj = MessagePackSerializer.Deserialize(clrType, payload, cancellationToken: ct);

                if (evtObj == null)
                {
                    // TODO Logging
                    continue;
                }
            }
            catch
            {
                // TODO: Logging
                continue;
            }

            foreach (var d in handlers)
            {
                try
                {
                    d.DynamicInvoke(evtObj);
                }
                catch
                { /* swallow for now */
                }
            }
        }
    }

    private class Subscription : IDisposable
    {
        private readonly Action _Dispose;

        public Subscription(Action dispose) => _Dispose = dispose;

        public void Dispose() => _Dispose();
    }
}
