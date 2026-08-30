// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Collections.Concurrent;
using FoosVision.Common.Logging;
using FoosVision.NetMq.Internal;
using FoosVision.Protocol;
using FoosVision.Protocol.Connectivity.Abstractions;
using MessagePack;
using NetMQ.Sockets;

namespace FoosVision.NetMq;

public class RecorderLiveAnalysisSubscriber : IRecorderLiveAnalysisSubscriber
{
    private static readonly Source _Log = new("NetMq.LiveAnalysisSubscriber");

    private readonly SubscriberSocket _Socket = new();
    private readonly ConcurrentDictionary<LiveAnalysisMessageType, ConcurrentBag<Delegate>> _Handlers = new();

    private readonly CancellationTokenSource _Cts = new();
    private readonly TimeSpan _PollTimeout = TimeSpan.FromMilliseconds(250);
    private readonly Task _LoopTask;

    public RecorderLiveAnalysisSubscriber(string recorderLiveAnalysisAddress)
    {
        _Socket.Connect(recorderLiveAnalysisAddress);
        _Socket.Subscribe([]);
        _LoopTask = Task.Run(() => Loop(_Cts.Token));
    }

    public IDisposable Subscribe<TMessage>(Action<TMessage> onMessage)
    {
        var type = ProtocolTypeRegistry.GetLiveAnalysisMessageType<TMessage>();
        var bag = _Handlers.GetOrAdd(type, _ => []);
        bag.Add(onMessage);

        return new Subscription(() => { });
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
            if (!Frames.TryReceive(out var typeByte, out var payload, _Socket, _PollTimeout))
            {
                continue;
            }

            var type = (LiveAnalysisMessageType)typeByte;

            if (!_Handlers.TryGetValue(type, out var handlers) || handlers.IsEmpty)
            {
                continue;
            }

            var clrType = ProtocolTypeRegistry.GetLiveAnalysisMessageClrType(type);
            object? messageObj;

            try
            {
                messageObj = MessagePackSerializer.Deserialize(clrType, payload, cancellationToken: ct);

                if (messageObj == null)
                {
                    continue;
                }
            }
            catch (Exception ex)
            {
                _Log.Warning("LiveAnalysis deserialize failed. Type={Type} Ex={Exception}", type, ex.ToString());
                continue;
            }

            foreach (var handler in handlers)
            {
                try
                {
                    handler.DynamicInvoke(messageObj);
                }
                catch (Exception ex)
                {
                    _Log.Warning(
                        "LiveAnalysis handler failed. Type={Type} Handler={Handler} Ex={Exception}",
                        type,
                        handler.Method.Name,
                        ex.ToString());
                }
            }
        }
    }

    private class Subscription : IDisposable
    {
        private readonly Action _Dispose;

        public Subscription(Action dispose)
        {
            _Dispose = dispose;
        }

        public void Dispose()
        {
            _Dispose();
        }
    }
}
