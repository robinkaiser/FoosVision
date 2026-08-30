// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace FoosVision.Media.Core.EncodedVideoStreaming;

internal static class LocalRtpEndpointResolver
{
    public static IPAddress? PickLocalIPv4ForRemote(IPAddress remoteAddress)
    {
        ArgumentNullException.ThrowIfNull(remoteAddress);

        if (remoteAddress.AddressFamily != AddressFamily.InterNetwork)
        {
            return null;
        }

        List<LocalIPv4Address> localAddresses = GetLocalIPv4Addresses();
        IPAddress? sameSubnetAddress = PickSameSubnetAddress(remoteAddress, localAddresses);
        if (sameSubnetAddress is not null)
        {
            return sameSubnetAddress;
        }

        return PickLocalIPv4UsingRoute(remoteAddress);
    }

    internal static IPAddress? PickSameSubnetAddress(
        IPAddress remoteAddress,
        IEnumerable<LocalIPv4Address> localAddresses)
    {
        ArgumentNullException.ThrowIfNull(remoteAddress);
        ArgumentNullException.ThrowIfNull(localAddresses);

        if (remoteAddress.AddressFamily != AddressFamily.InterNetwork)
        {
            return null;
        }

        foreach (LocalIPv4Address localAddress in localAddresses)
        {
            if (IsUsableAddress(localAddress.Address) &&
                IsSameSubnet(localAddress.Address, remoteAddress, localAddress.Mask))
            {
                return localAddress.Address;
            }
        }

        return null;
    }

    private static List<LocalIPv4Address> GetLocalIPv4Addresses()
    {
        List<LocalIPv4Address> addresses = [];

        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus is not OperationalStatus.Up and
                not OperationalStatus.Unknown)
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

            foreach (UnicastIPAddressInformation unicastAddress in properties.UnicastAddresses)
            {
                IPAddress address = unicastAddress.Address;
                IPAddress? mask = GetIPv4Mask(unicastAddress);
                if (address.AddressFamily == AddressFamily.InterNetwork &&
                    mask is not null &&
                    IsUsableAddress(address))
                {
                    addresses.Add(new LocalIPv4Address(address, mask));
                }
            }
        }

        return addresses;
    }

    private static IPAddress? GetIPv4Mask(UnicastIPAddressInformation unicastAddress)
    {
        try
        {
            return unicastAddress.IPv4Mask;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static IPAddress? PickLocalIPv4UsingRoute(IPAddress remoteAddress)
    {
        try
        {
            // UDP connect does not send packets; it asks the OS which local endpoint it would use.
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(new IPEndPoint(remoteAddress, 9));

            if (socket.LocalEndPoint is not IPEndPoint local ||
                !IsUsableAddress(local.Address))
            {
                return null;
            }

            return local.Address;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static bool IsSameSubnet(IPAddress localAddress, IPAddress remoteAddress, IPAddress mask)
    {
        byte[] localBytes = localAddress.GetAddressBytes();
        byte[] remoteBytes = remoteAddress.GetAddressBytes();
        byte[] maskBytes = mask.GetAddressBytes();

        if (localBytes.Length != remoteBytes.Length ||
            localBytes.Length != maskBytes.Length)
        {
            return false;
        }

        for (int i = 0; i < localBytes.Length; i++)
        {
            if ((localBytes[i] & maskBytes[i]) != (remoteBytes[i] & maskBytes[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUsableAddress(IPAddress address)
    {
        return !IPAddress.IsLoopback(address) &&
            !IsLinkLocalIPv4(address);
    }

    private static bool IsLinkLocalIPv4(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return bytes.Length == 4 &&
            bytes[0] == 169 &&
            bytes[1] == 254;
    }

    internal readonly record struct LocalIPv4Address(IPAddress Address, IPAddress Mask);
}
