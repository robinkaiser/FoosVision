// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Protocol.Connectivity.Configuration;
using FoosVision.Protocol.Connectivity.Discovery;
using FoosVision.Protocol.Messages.Common;
using NetDiscovery;
using NetDiscovery.Udp;

namespace FoosVision.NetDiscovery;

public class UdpRecorderDiscovery : IRecorderDiscovery
{
    public UdpRecorderDiscovery()
    {
    }

    public IRecorderDiscoverySession Start()
    {
        return new UdpRecorderDiscoverySession();
    }

    private sealed class UdpRecorderDiscoverySession : IRecorderDiscoverySession
    {
        private readonly UdpProvider _UdpProvider;
        private readonly IClient _DiscoveryClient;
        private readonly ConcurrentDictionary<IPAddress, RecorderDiscoveryCandidate> _Candidates = new();

        public UdpRecorderDiscoverySession()
        {
            _UdpProvider = new UdpProvider(DefaultPorts.DiscoveryUdp);
            _DiscoveryClient = _UdpProvider.CreateClient();

            _DiscoveryClient.Discovery += OnDiscovery;
            _DiscoveryClient.Start();
            _UdpProvider.Start();
        }

        public IReadOnlyList<RecorderDiscoveryCandidate> GetCandidatesRankedSnapshot()
        {
            return [.. _Candidates
                .OrderBy(x => x.Key, Comparer<IPAddress>.Create(TcpAddressTools.CompareCandidates))
                .Select(x => x.Value)];
        }

        public void Dispose()
        {
            try
            {
                _DiscoveryClient.Discovery -= OnDiscovery;
                _DiscoveryClient.Stop();
                _UdpProvider.Stop();
                _DiscoveryClient.Dispose();
                _UdpProvider.Dispose();
            }
            catch
            {
            }
        }

        private void OnDiscovery(object? sender, DiscoveryEventArgs e)
        {
            if (e.Address is null) return;

            // IPv4 only
            if (e.Address.AddressFamily != AddressFamily.InterNetwork) return;

            // Ignore obvious non-routable addresses
            if (IPAddress.IsLoopback(e.Address)) return;
            if (TcpAddressTools.IsLinkLocalIPv4(e.Address)) return;

            if (!DiscoveryIdentity.TryParseRecorderIdentity(e.Identity, out var identity)) return;

            // Quick rejection for incompatible protocol
            if (identity.ProtocolVersion != ProtocolVersions.Current) return;

            var candidate = new RecorderDiscoveryCandidate(
                RecorderIpAddress: e.Address.ToString(),
                RecorderAppVersion: identity.AppVersion,
                ProtocolVersion: identity.ProtocolVersion);

            _Candidates.TryAdd(e.Address, candidate);
        }
    }
}
