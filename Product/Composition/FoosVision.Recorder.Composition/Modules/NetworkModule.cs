// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Net;
using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.Common.Logging;
using FoosVision.Common.Metrics;
using FoosVision.NetMq;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Connectivity.Configuration;
using FoosVision.Protocol.Connectivity.Discovery;
using FoosVision.Protocol.Messages.Common;
using FoosVision.Protocol.Messages.Handshake;
using NetDiscovery;
using NetDiscovery.Udp;

namespace FoosVision.Recorder.Composition.Modules;

internal class NetworkModule : IDisposable
{
    private static readonly Source _Log = new("Recorder.Composition.NetworkModule");

    private readonly UdpProvider _UdpProvider;
    private readonly IServer _DiscoveryServer;

    private readonly HandshakeServerHost _HandshakeHost;
    private readonly RecorderEventPublisher _EventPublisher;
    private readonly RecorderLiveDataPublisher _LiveDataPublisher;
    private readonly RecorderLiveAnalysisPublisher _LiveAnalysisPublisher;
    private readonly Action<HelloRequest>? _OnHello;
    private RecorderCommandServerHost? _CommandHost;

    private int _DiscoveryRunning;
    private int _DiscoveryAnnouncementLogged;
    private int _ViewerConnected;
    private bool _Started;

    public NetworkModule(
        IRecorderVersionProvider versionProvider,
        IHandshakeDiagnosticsProvider diagnosticsProvider,
        IHandshakeViewerSettingsProvider viewerSettingsProvider,
        RuntimeMetricsOptions? runtimeMetricsOptions = null,
        Action<HelloRequest>? onHello = null)
    {
        var discoveryPort = DefaultPorts.DiscoveryUdp;
        var appVersion = versionProvider.GetAppVersion();
        var identity = DiscoveryIdentity.BuildRecorderIdentity(ProtocolVersions.Current, appVersion);

        _OnHello = onHello;
        _UdpProvider = new UdpProvider(discoveryPort, OnDiscoveryAnnouncementSent);
        _DiscoveryServer = _UdpProvider.CreateServer();
        _DiscoveryServer.Identity = identity;

        var handshakeController = new HandshakeHandler(
            versionProvider,
            diagnosticsProvider,
            viewerSettingsProvider,
            TryAcceptViewer);
        _HandshakeHost = new HandshakeServerHost(handshakeController);

        _EventPublisher = new RecorderEventPublisher();
        _LiveDataPublisher = new RecorderLiveDataPublisher(runtimeMetricsOptions);
        _LiveAnalysisPublisher = new RecorderLiveAnalysisPublisher();
    }

    public IRecorderEventPublisher EventPublisher => _EventPublisher;

    public IRecorderLiveDataPublisher LiveDataPublisher => _LiveDataPublisher;

    public IRecorderLiveAnalysisPublisher LiveAnalysisPublisher => _LiveAnalysisPublisher;

    public void SetCommandRouter(IRecorderCommandRouter router)
    {
        _CommandHost ??= new RecorderCommandServerHost(router);
    }

    public void Start()
    {
        if (_Started) return;

        if (_CommandHost is null)
        {
            throw new InvalidOperationException("Command router not set!");
        }

        StartDiscovery();

        var handShakeBind = $"tcp://*:{DefaultPorts.HandshakeReqRepTcp}";
        _HandshakeHost.Start(handShakeBind);
        _Log.Information("Recorder handshake endpoint started. BindAddress={0}", handShakeBind);

        var commandsBind = $"tcp://*:{DefaultPorts.CommandsReqRepTcp}";
        _CommandHost.Start(commandsBind);

        var eventsBind = $"tcp://*:{DefaultPorts.EventsPubSubTcp}";
        _EventPublisher.Bind(eventsBind);

        var liveDataBind = $"tcp://*:{DefaultPorts.LiveDataPubSubTcp}";
        _LiveDataPublisher.Bind(liveDataBind);

        var liveAnalysisBind = $"tcp://*:{DefaultPorts.LiveAnalysisPubSubTcp}";
        _LiveAnalysisPublisher.Bind(liveAnalysisBind);

        _Started = true;
    }

    public void ReleaseViewerConnection()
    {
        if (Interlocked.CompareExchange(ref _ViewerConnected, 0, 1) != 1)
        {
            return;
        }

        _Log.Information("Recorder viewer connection released. Discovery remains active.");
        StartDiscovery();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            _LiveDataPublisher.Dispose();
            _LiveAnalysisPublisher.Dispose();
            _EventPublisher.Dispose();

            _CommandHost?.Dispose();
            _HandshakeHost.Dispose();

            _DiscoveryServer.Dispose();
            _UdpProvider.Dispose();
        }
        catch
        {
        }
    }

    private void OnDiscoveryAnnouncementSent(IPAddress localAddress, IPAddress targetAddress, int port, string identity)
    {
        if (Interlocked.Exchange(ref _DiscoveryAnnouncementLogged, 1) != 0)
        {
            return;
        }

        _Log.Information(
            "Recorder discovery announcement sent. LocalAddress={0} TargetAddress={1} Port={2} Identity={3}",
            localAddress,
            targetAddress,
            port,
            identity);
    }

    private bool TryAcceptViewer(HelloRequest request)
    {
        if (Interlocked.CompareExchange(ref _ViewerConnected, 1, 0) != 0)
        {
            _Log.Warning(
                "Recorder rejected viewer handshake because another viewer is already connected. ViewerAddress={0}",
                request.ViewerIpAddress);
            return false;
        }

        _OnHello?.Invoke(request);
        return true;
    }

    private void StartDiscovery()
    {
        if (Interlocked.CompareExchange(ref _DiscoveryRunning, 1, 0) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref _DiscoveryAnnouncementLogged, 0);
        _DiscoveryServer.Start();
        _UdpProvider.Start();
        _Log.Information(
            "Recorder discovery started. Port={0} Identity={1}",
            DefaultPorts.DiscoveryUdp,
            _DiscoveryServer.Identity);
    }

}
