// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Common.Metrics;
using FoosVision.Protocol;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Live;
using MessagePack;
using NetMQ;
using NetMQ.Sockets;

namespace FoosVision.NetMq;

public class RecorderLiveDataPublisher : IRecorderLiveDataPublisher, IDisposable
{
    private static readonly Source _MetricsLog = new("NetMq.LiveDataPublisher");

    private readonly PublisherSocket _Socket = new();
    private readonly Lock _SocketLock = new();
    private readonly IntervalMetric? _TrackingFrameSendInterval;

    public RecorderLiveDataPublisher(RuntimeMetricsOptions? runtimeMetricsOptions = null)
    {
        RuntimeMetricsOptions options = runtimeMetricsOptions ?? RuntimeMetricsOptions.CreateDefault();

        if (options.Enabled)
        {
            _TrackingFrameSendInterval = new IntervalMetric(
                options.CreateMetricName("Recorder.NetMq.TrackingFrameSendInterval"),
                _MetricsLog,
                options.GetReportInterval());
        }
    }

    public void Bind(string bindAddress)
    {
        _Socket.Bind(bindAddress);
    }

    public Task PublishTrackingFrame(TrackingFrameMessage frame, CancellationToken ct = default)
    {
        _TrackingFrameSendInterval?.Record();
        return Publish(frame, ct);
    }

    public Task PublishTableUpdate(TableUpdateMessage update, CancellationToken ct = default)
        => Publish(update, ct);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _Socket.Dispose();
    }

    private Task Publish<TMessage>(TMessage message, CancellationToken ct)
    {
        var type = ProtocolTypeRegistry.GetLiveMessageType<TMessage>();
        var payload = MessagePackSerializer.Serialize(message, cancellationToken: ct);

        lock (_SocketLock)
        {
            _Socket.SendMoreFrame([(byte)type]);
            _Socket.SendFrame(payload);
        }

        return Task.CompletedTask;
    }
}
