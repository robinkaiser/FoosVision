// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Android.Content;
using Android.Net;
using Android.Net.Wifi;
using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Common.Logging;
using FoosVision.Protocol.Connectivity.Configuration;
using FoosVision.Protocol.Messages.Common;

namespace FoosVision.Viewer.App.Platforms.Android.Connectivity;

internal class AndroidRecorderFallbackCandidateSource : IRecorderFallbackCandidateSource
{
    private const int _MaxConcurrentProbes = 32;
    private static readonly TimeSpan _ProbeTimeout = TimeSpan.FromMilliseconds(180);
    private static readonly Source _Log = new("Viewer.Android.Connectivity.AndroidRecorderFallbackCandidateSource");

    private readonly Context _Context;

    public AndroidRecorderFallbackCandidateSource(Context context)
    {
        _Context = context.ApplicationContext ?? context;
    }

    public async Task<IReadOnlyList<RecorderDiscoveryCandidate>> GetCandidatesAsync(CancellationToken ct)
    {
        List<IPAddress> localAddresses = GetLocalWifiIPv4Addresses();
        _Log.Information(
            "Android recorder fallback local WiFi addresses. Addresses={0}",
            localAddresses.Count == 0 ? "<none>" : string.Join(",", localAddresses.Select(x => x.ToString())));

        List<IPAddress> probeAddresses = GetProbeAddresses(localAddresses);
        if (probeAddresses.Count == 0)
        {
            _Log.Warning("Android recorder fallback skipped because no local WiFi probe addresses were available.");
            return [];
        }

        _Log.Information(
            "Android recorder fallback probing local subnet for recorder handshake endpoint. AddressCount={0} Port={1}",
            probeAddresses.Count,
            DefaultPorts.HandshakeReqRepTcp);

        ConcurrentBag<RecorderDiscoveryCandidate> candidates = new();

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
                    RecorderAppVersion: "android-direct-probe",
                    ProtocolVersion: ProtocolVersions.Current));
            });

        IReadOnlyList<RecorderDiscoveryCandidate> result = [.. candidates
            .OrderBy(x => IPAddress.Parse(x.RecorderIpAddress), Comparer<IPAddress>.Create(CompareAddresses))];

        if (result.Count > 0)
        {
            _Log.Information(
                "Android recorder fallback found handshake endpoints. Count={0} Addresses={1}",
                result.Count,
                string.Join(",", result.Select(x => x.RecorderIpAddress)));
        }

        return result;
    }

    private List<IPAddress> GetLocalWifiIPv4Addresses()
    {
        List<IPAddress> addresses = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        AddConnectivityManagerAddresses(addresses, seen);
        AddWifiManagerAddress(addresses, seen);

        return addresses;
    }

    private void AddConnectivityManagerAddresses(List<IPAddress> addresses, HashSet<string> seen)
    {
        try
        {
            var connectivityManager = _Context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
            Network? activeNetwork = connectivityManager?.ActiveNetwork;
            if (connectivityManager is null ||
                activeNetwork is null)
            {
                return;
            }

            NetworkCapabilities? capabilities = connectivityManager.GetNetworkCapabilities(activeNetwork);
            if (capabilities is null ||
                !capabilities.HasTransport(TransportType.Wifi))
            {
                return;
            }

            LinkProperties? linkProperties = connectivityManager.GetLinkProperties(activeNetwork);
            if (linkProperties is null)
            {
                return;
            }

            foreach (LinkAddress linkAddress in linkProperties.LinkAddresses)
            {
                AddLocalAddress(addresses, seen, ToIPAddress(linkAddress.Address));
            }
        }
        catch (Exception ex)
        {
            _Log.Warning("Could not read Android ConnectivityManager WiFi addresses: {0}", ex);
        }
    }

    private void AddWifiManagerAddress(List<IPAddress> addresses, HashSet<string> seen)
    {
        try
        {
#pragma warning disable CA1422
            var wifiManager = _Context.GetSystemService(Context.WifiService) as WifiManager;
            int? ipAddress = wifiManager?.ConnectionInfo?.IpAddress;
#pragma warning restore CA1422
            if (ipAddress is null ||
                ipAddress.Value == 0)
            {
                return;
            }

            byte[] bytes = BitConverter.GetBytes(ipAddress.Value);
            AddLocalAddress(addresses, seen, new IPAddress(bytes));
        }
        catch (Exception ex)
        {
            _Log.Warning("Could not read Android WifiManager IP address: {0}", ex);
        }
    }

    private static IPAddress? ToIPAddress(Java.Net.InetAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        byte[]? bytes = address.GetAddress();
        if (bytes is null ||
            bytes.Length != 4)
        {
            return null;
        }

        return new IPAddress(bytes);
    }

    private static List<IPAddress> GetProbeAddresses(IReadOnlyList<IPAddress> localAddresses)
    {
        List<IPAddress> addresses = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (IPAddress localAddress in localAddresses)
        {
            byte[] localBytes = localAddress.GetAddressBytes();
            for (int host = 1; host <= 254; host++)
            {
                IPAddress candidate = new([localBytes[0], localBytes[1], localBytes[2], (byte)host]);
                if (candidate.Equals(localAddress))
                {
                    continue;
                }

                AddRemoteAddress(addresses, seen, candidate);
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

    private static void AddLocalAddress(List<IPAddress> addresses, HashSet<string> seen, IPAddress? address)
    {
        if (address is null ||
            !CanUseLocalIPv4(address))
        {
            return;
        }

        string key = address.ToString();
        if (seen.Add(key))
        {
            addresses.Add(address);
        }
    }

    private static void AddRemoteAddress(List<IPAddress> addresses, HashSet<string> seen, IPAddress address)
    {
        if (!CanUseRemoteIPv4(address))
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
