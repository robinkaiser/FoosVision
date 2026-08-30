// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Common.Metrics;
using FoosVision.Common.Types;
using FoosVision.NetDiscovery;
using FoosVision.NetMq;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Connectivity.Configuration;

namespace FoosVision.Viewer.Composition.Modules;

internal class NetworkModule : IDisposable
{
    private readonly HandshakeClient _HandshakeClient;
    private readonly UdpRecorderDiscovery _RecorderDiscovery;
    private readonly RecorderConnectionService _ConnectionService;
    private Option<ConnectedNetworkSession> _ConnectedSession = Option<ConnectedNetworkSession>.None();

    public NetworkModule(
        RecorderConnectionOptions? connectionOptions,
        IRecorderFallbackCandidateSource? fallbackCandidateSource)
    {
        var options = connectionOptions ?? RecorderConnectionOptions.Default;

        _HandshakeClient = new HandshakeClient();
        _RecorderDiscovery = new UdpRecorderDiscovery();
        _ConnectionService = new RecorderConnectionService(
            _RecorderDiscovery,
            _HandshakeClient,
            options,
            fallbackCandidateSource);
    }

    public RecorderConnection Connection => _ConnectedSession.Value.Connection;

    public IRecorderCommandClient CommandClient => _ConnectedSession.Value.CommandClient;

    public IRecorderEventSubscriber EventSubscriber => _ConnectedSession.Value.EventSubscriber;

    public IRecorderLiveDataSubscriber LiveDataSubscriber => _ConnectedSession.Value.LiveDataSubscriber;

    public IRecorderLiveAnalysisSubscriber LiveAnalysisSubscriber => _ConnectedSession.Value.LiveAnalysisSubscriber;

    public async Task<RecorderConnectionResult> ConnectAsync(CancellationToken ct)
    {
        if (_ConnectedSession.TryGetValue(out var existing))
        {
            return RecorderConnectionResult.Connected(existing.Connection);
        }

        var result = await _ConnectionService.ConnectAsync(ct);
        if (!result.Success)
        {
            return result;
        }

        var connection = result.Connection.Value;
        var commandsAddress = $"tcp://{connection.RecorderIpAddress}:{DefaultPorts.CommandsReqRepTcp}";
        var eventsAddress = $"tcp://{connection.RecorderIpAddress}:{DefaultPorts.EventsPubSubTcp}";
        var liveDataAddress = $"tcp://{connection.RecorderIpAddress}:{DefaultPorts.LiveDataPubSubTcp}";
        var liveAnalysisAddress = $"tcp://{connection.RecorderIpAddress}:{DefaultPorts.LiveAnalysisPubSubTcp}";

        RuntimeMetricsOptions runtimeMetricsOptions = CreateRuntimeMetricsOptions(connection);
        var commandClient = new RecorderCommandClient(commandsAddress);
        var eventSubscriber = new RecorderEventSubscriber(eventsAddress);
        var liveDataSubscriber = new RecorderLiveDataSubscriber(liveDataAddress, runtimeMetricsOptions);
        var liveAnalysisSubscriber = new RecorderLiveAnalysisSubscriber(liveAnalysisAddress);

        _ConnectedSession = Option<ConnectedNetworkSession>.Some(
            new ConnectedNetworkSession(connection, commandClient, eventSubscriber, liveDataSubscriber, liveAnalysisSubscriber));

        return result;
    }

    public void Disconnect()
    {
        if (!_ConnectedSession.TryGetValue(out var connectedSession))
        {
            return;
        }

        _ConnectedSession = Option<ConnectedNetworkSession>.None();
        connectedSession.Dispose();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        Disconnect();
        _HandshakeClient.Dispose();
    }

    private static RuntimeMetricsOptions CreateRuntimeMetricsOptions(RecorderConnection connection)
    {
        var runtimeMetrics = connection.Diagnostics.RuntimeMetrics;
        return new RuntimeMetricsOptions
        {
            Enabled = runtimeMetrics.Enabled,
            ReportInterval = TimeSpan.FromSeconds(Math.Max(1, runtimeMetrics.ReportIntervalSeconds)),
            NamePrefix = "Android",
        };
    }

    private sealed class ConnectedNetworkSession : IDisposable
    {
        public ConnectedNetworkSession(
            RecorderConnection connection,
            RecorderCommandClient commandClient,
            RecorderEventSubscriber eventSubscriber,
            RecorderLiveDataSubscriber liveDataSubscriber,
            RecorderLiveAnalysisSubscriber liveAnalysisSubscriber)
        {
            Connection = connection;
            CommandClient = commandClient;
            EventSubscriber = eventSubscriber;
            LiveDataSubscriber = liveDataSubscriber;
            LiveAnalysisSubscriber = liveAnalysisSubscriber;
        }

        public RecorderConnection Connection { get; }

        public RecorderCommandClient CommandClient { get; }

        public RecorderEventSubscriber EventSubscriber { get; }

        public RecorderLiveDataSubscriber LiveDataSubscriber { get; }

        public RecorderLiveAnalysisSubscriber LiveAnalysisSubscriber { get; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            try
            {
                LiveAnalysisSubscriber.Dispose();
                LiveDataSubscriber.Dispose();
                EventSubscriber.Dispose();
                CommandClient.Dispose();
            }
            catch
            {
            }
        }
    }
}
