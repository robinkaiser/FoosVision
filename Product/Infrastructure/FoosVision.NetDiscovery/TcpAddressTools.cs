// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace FoosVision.NetDiscovery;

public static class TcpAddressTools
{
    public static bool IsLinkLocalIPv4(IPAddress ip)
    {
        // 169.254.0.0/16
        var bytes = ip.GetAddressBytes();

        return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
    }

    // Returns true if the candidate is in the same subnet as any active local IPv4 interface.
    // Cross-platform using NetworkInterface; if masks are unavailable, returns false.
    public static bool IsInSameSubnetAsLocalInterface(IPAddress candidate)
    {
        if (candidate.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        foreach (var (address, mask) in GetLocalIPv4Subnets())
        {
            if (IsInSubnet(candidate, address, mask))
            {
                return true;
            }
        }

        return false;
    }

    // Prefer candidates:
    //  1) Same subnet as local interface
    //  2) Typical private LAN ranges: 192.168/16, then 10/8, then 172.16-31/12
    //  3) Stable tie-breaker by byte comparison
    public static int CompareCandidates(IPAddress left, IPAddress right)
    {
        var leftSameSubnet = IsInSameSubnetAsLocalInterface(left);
        var rightSameSubnet = IsInSameSubnetAsLocalInterface(right);

        if (leftSameSubnet != rightSameSubnet)
        {
            return leftSameSubnet ? -1 : 1;
        }

        var leftScore = PrivateRangeScore(left);
        var rightScore = PrivateRangeScore(right);

        if (leftScore != rightScore)
        {
            return leftScore.CompareTo(rightScore);
        }

        // Stable tie-breaker: compare bytes
        var leftBytes = left.GetAddressBytes();
        var rightBytes = right.GetAddressBytes();

        for (var i = 0; i < 4; i++)
        {
            var compare = leftBytes[i].CompareTo(rightBytes[i]);
            if (compare != 0)
            {
                return compare;
            }
        }

        return 0;
    }

    // Check for IPv4 address in the RFC1918 private ranges
    private static int PrivateRangeScore(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();

        if (bytes.Length != 4)
        {
            return 99;
        }

        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168) return 0;

        // 10.0.0.0/8
        if (bytes[0] == 10) return 1;

        // 172.16.0.0/12
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return 2;

        return 50;
    }

    private static IEnumerable<(IPAddress Address, IPAddress Mask)> GetLocalIPv4Subnets()
    {
        // Only consider "up" interfaces that are not loopback.
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up) continue;
            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            IPInterfaceProperties properties;
            try
            {
                properties = networkInterface.GetIPProperties();
            }
            catch
            {
                continue;
            }

            foreach (var unicastAddress in properties.UnicastAddresses)
            {
                if (unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                // Some platforms may not provide IPv4Mask; skip if missing.
                if (unicastAddress.IPv4Mask is null) continue;

                yield return (unicastAddress.Address, unicastAddress.IPv4Mask);
            }
        }
    }

    private static bool IsInSubnet(IPAddress ip, IPAddress subnetAddress, IPAddress subnetMask)
    {
        var ipBytes = ip.GetAddressBytes();
        var subnetBytes = subnetAddress.GetAddressBytes();
        var maskBytes = subnetMask.GetAddressBytes();

        if (ipBytes.Length != 4 || subnetBytes.Length != 4 || maskBytes.Length != 4)
        {
            return false;
        }

        for (var i = 0; i < 4; i++)
        {
            if ((ipBytes[i] & maskBytes[i]) != (subnetBytes[i] & maskBytes[i]))
            {
                return false;
            }
        }

        return true;
    }
}
