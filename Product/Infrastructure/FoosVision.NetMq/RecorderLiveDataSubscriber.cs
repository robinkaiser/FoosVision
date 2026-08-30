// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Collections.Concurrent;
using FoosVision.Common.Logging;
using FoosVision.Common.Metrics;
using FoosVision.NetMq.Internal;
using FoosVision.Protocol;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Live;
using MessagePack;
using NetMQ.Sockets;

namespace FoosVision.NetMq;

public class RecorderLiveDataSubscriber : IRecorderLiveDataSubscriber
{
    private static readonly Source _Log = new("NetMq.LiveDataSubscriber");

    private readonly SubscriberSocket _Socket = new();
    private readonly ConcurrentDictionary<LiveMessageType, ConcurrentBag<Delegate>> _Handlers = new();
    private readonly IntervalMetric? _TrackingFrameReceiveInterval;

    private readonly CancellationTokenSource _Cts = new();
    private readonly TimeSpan _PollTimeout = TimeSpan.FromMilliseconds(250);
    private readonly Task _LoopTask;

    public RecorderLiveDataSubscriber(
        string recorderLiveDataAddress,
        RuntimeMetricsOptions? runtimeMetricsOptions = null)
    {
        RuntimeMetricsOptions options = runtimeMetricsOptions ?? RuntimeMetricsOptions.CreateDefault();

        if (options.Enabled)
        {
            _TrackingFrameReceiveInterval = new IntervalMetric(
                options.CreateMetricName("Viewer.NetMq.TrackingFrameReceiveInterval"),
                _Log,
                options.GetReportInterval());
        }

        _Socket.Connect(recorderLiveDataAddress);
        _Socket.Subscribe([]);
        _LoopTask = Task.Run(() => Loop(_Cts.Token));
    }

    public IDisposable Subscribe<TMessage>(Action<TMessage> onMessage)
    {
        var type = ProtocolTypeRegistry.GetLiveMessageType<TMessage>();
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

            var type = (LiveMessageType)typeByte;

            if (!_Handlers.TryGetValue(type, out var handlers) || handlers.IsEmpty)
            {
                continue;
            }

            var clrType = ProtocolTypeRegistry.GetLiveMessageClrType(type);
            object? messageObj;

            try
            {
                messageObj = MessagePackSerializer.Deserialize(clrType, payload, cancellationToken: ct);

                if (messageObj == null)
                {
                    continue;
                }

                if (type == ProtocolTypeRegistry.GetLiveMessageType<TrackingFrameMessage>())
                {
                    _TrackingFrameReceiveInterval?.Record();
                }
            }
            catch (Exception ex)
            {
                _Log.Warning("LiveData deserialize failed. Type={Type} Ex={Exception}", type, ex.ToString());
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
                        "LiveData handler failed. Type={Type} Handler={Handler} Ex={Exception}",
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
