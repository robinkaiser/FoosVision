// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using FoosVision.Common.Logging;
using FoosVision.Protocol.Connectivity.Configuration;
using FoosVision.Protocol.Messages.Common;

namespace FoosVision.Adapters.Viewer.Connectivity;

internal class LocalSubnetRecorderFallbackCandidateSource : IRecorderFallbackCandidateSource
{
    private const int _MaxConcurrentProbes = 32;
    private static readonly TimeSpan _ProbeTimeout = TimeSpan.FromMilliseconds(180);
    private static readonly Source _Log = new("Adapters.Viewer.Connectivity.LocalSubnetRecorderFallbackCandidateSource");

    public async Task<IReadOnlyList<RecorderDiscoveryCandidate>> GetCandidatesAsync(CancellationToken ct)
    {
        IReadOnlyList<IPAddress> probeAddresses = GetProbeAddresses();
        if (probeAddresses.Count == 0)
        {
            return [];
        }

        _Log.Information(
            "Probing local subnet for recorder handshake endpoint. AddressCount={0} Port={1}",
            probeAddresses.Count,
            DefaultPorts.HandshakeReqRepTcp);

        ConcurrentBag<RecorderDiscoveryCandidate> candidates = new();

        try
        {
            await Parallel.ForEachAsync(
                probeAddresses,
                new ParallelOptions
                {
                    CancellationToken = ct,
                    MaxDegreeOfParallelism = _MaxConcurrentProbes,
                },
                async (address, token) =>
                {
                    if (!await CanConnectAsync(address, token))
                    {
                        return;
                    }

                    candidates.Add(new RecorderDiscoveryCandidate(
                        RecorderIpAddress: address.ToString(),
                        RecorderAppVersion: "direct-probe",
                        ProtocolVersion: ProtocolVersions.Current));
                });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }

        IReadOnlyList<RecorderDiscoveryCandidate> result = [.. candidates
            .OrderBy(x => IPAddress.Parse(x.RecorderIpAddress), Comparer<IPAddress>.Create(CompareAddresses))];

        if (result.Count > 0)
        {
            _Log.Information(
                "Local subnet recorder probe found handshake endpoints. Count={0}",
                result.Count);
        }

        return result;
    }

    internal static IReadOnlyList<IPAddress> GetProbeAddresses()
    {
        List<IPAddress> addresses = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            IPInterfaceProperties properties;
            try
            {
                properties = networkInterface.GetIPProperties();
            }
            catch
            {
                continue;
            }

            foreach (GatewayIPAddressInformation gateway in properties.GatewayAddresses)
            {
                AddProbeAddress(addresses, seen, gateway.Address);
            }

            foreach (UnicastIPAddressInformation unicastAddress in properties.UnicastAddresses)
            {
                IPAddress localAddress = unicastAddress.Address;
                if (!CanUseLocalIPv4(localAddress))
                {
                    continue;
                }

                byte[] localBytes = localAddress.GetAddressBytes();
                for (int host = 1; host <= 254; host++)
                {
                    IPAddress candidate = new([localBytes[0], localBytes[1], localBytes[2], (byte)host]);
                    if (candidate.Equals(localAddress))
                    {
                        continue;
                    }

                    AddProbeAddress(addresses, seen, candidate);
                }
            }
        }

        return addresses;
    }

    private static async Task<bool> CanConnectAsync(IPAddress address, CancellationToken ct)
    {
        using CancellationTokenSource probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probeCts.CancelAfter(_ProbeTimeout);

        try
        {
            using TcpClient client = new(address.AddressFamily);
            await client.ConnectAsync(address, DefaultPorts.HandshakeReqRepTcp, probeCts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static void AddProbeAddress(List<IPAddress> addresses, HashSet<string> seen, IPAddress? address)
    {
        if (address is null ||
            !CanUseRemoteIPv4(address))
        {
            return;
        }

        string key = address.ToString();
        if (seen.Add(key))
        {
            addresses.Add(address);
        }
    }

    private static bool CanUseLocalIPv4(IPAddress address)
    {
        return CanUseRemoteIPv4(address) &&
               !address.Equals(IPAddress.Any);
    }

    private static bool CanUseRemoteIPv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        if (IPAddress.IsLoopback(address))
        {
            return false;
        }

        byte[] bytes = address.GetAddressBytes();
        return bytes is not [0, 0, 0, 0] &&
               bytes is not [255, 255, 255, 255] &&
               !IsLinkLocalIPv4(bytes);
    }

    private static bool IsLinkLocalIPv4(byte[] bytes)
    {
        return bytes.Length == 4 &&
               bytes[0] == 169 &&
               bytes[1] == 254;
    }

    private static int CompareAddresses(IPAddress left, IPAddress right)
    {
        byte[] leftBytes = left.GetAddressBytes();
        byte[] rightBytes = right.GetAddressBytes();

        for (int i = 0; i < 4; i++)
        {
            int compare = leftBytes[i].CompareTo(rightBytes[i]);
            if (compare != 0)
            {
                return compare;
            }
        }

        return 0;
    }
}
