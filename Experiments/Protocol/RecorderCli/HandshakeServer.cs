// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.NetMq;
using FoosVision.Protocol.Connectivity.Configuration;
using FoosVision.Protocol.Connectivity.Discovery;
using FoosVision.Protocol.Messages.Common;
using NetDiscovery;
using NetDiscovery.Udp;

namespace RecorderCli;

public class HandshakeServer : IDisposable
{
    private readonly VersionProvider _VersionProvider;
    private readonly UdpProvider _UdpProvider;
    private readonly IServer _DiscoveryServer;
    private readonly HandshakeServerHost _HandshakeServerHost;
    private readonly string _BindAddress;

    public HandshakeServer()
    {
        var discoveryPort = DefaultPorts.DiscoveryUdp;
        var tcpPort = DefaultPorts.HandshakeReqRepTcp;
        _BindAddress = $"tcp://*:{tcpPort}";

        _VersionProvider = new VersionProvider();
        var appVersion = _VersionProvider.GetAppVersion();

        var identity = DiscoveryIdentity.BuildRecorderIdentity(ProtocolVersions.Current, appVersion);

        Console.WriteLine($"UDP discovery port : {discoveryPort}");
        Console.WriteLine($"TCP bind address   : {_BindAddress}");
        Console.WriteLine($"Identity           : {identity}");

        _UdpProvider = new UdpProvider(discoveryPort);
        _DiscoveryServer = _UdpProvider.CreateServer();
        _DiscoveryServer.Identity = identity;

        _DiscoveryServer.Start();
        _UdpProvider.Start();

        var handshakeController = new HandshakeHandler(
            _VersionProvider,
            new DefaultHandshakeDiagnosticsProvider(),
            new DefaultHandshakeViewerSettingsProvider(),
            hello =>
            {
                Console.WriteLine($"Hello from Viewer {hello.ViewerIpAddress}, Protocol verion: {hello.ProtocolVersion}");
                return true;
            });

        _HandshakeServerHost = new HandshakeServerHost(handshakeController);
    }

    public void Start()
    {
        _HandshakeServerHost.Start(_BindAddress);

        Console.WriteLine("Handshake server started. Press Ctrl+C to exit.");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _HandshakeServerHost.Dispose();
        _UdpProvider.Dispose();
        _DiscoveryServer.Dispose();
        _VersionProvider.Dispose();
    }
}
