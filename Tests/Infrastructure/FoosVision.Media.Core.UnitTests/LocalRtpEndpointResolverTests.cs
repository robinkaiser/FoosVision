// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Net;
using FoosVision.Media.Core.EncodedVideoStreaming;

namespace FoosVision.Media.Core.UnitTests;

public class LocalRtpEndpointResolverTests
{
    [Fact]
    public void PickSameSubnetAddress_returns_local_address_matching_remote_subnet()
    {
        IPAddress remoteAddress = IPAddress.Parse("192.168.44.23");
        LocalRtpEndpointResolver.LocalIPv4Address[] localAddresses =
        [
            new(IPAddress.Parse("10.0.0.5"), IPAddress.Parse("255.255.255.0")),
            new(IPAddress.Parse("192.168.44.10"), IPAddress.Parse("255.255.255.0")),
        ];

        IPAddress? result = LocalRtpEndpointResolver.PickSameSubnetAddress(remoteAddress, localAddresses);

        Assert.Equal(IPAddress.Parse("192.168.44.10"), result);
    }

    [Fact]
    public void PickSameSubnetAddress_ignores_loopback_and_link_local_addresses()
    {
        IPAddress remoteAddress = IPAddress.Parse("192.168.44.23");
        LocalRtpEndpointResolver.LocalIPv4Address[] localAddresses =
        [
            new(IPAddress.Parse("127.0.0.1"), IPAddress.Parse("255.0.0.0")),
            new(IPAddress.Parse("169.254.10.12"), IPAddress.Parse("255.255.0.0")),
            new(IPAddress.Parse("192.168.44.10"), IPAddress.Parse("255.255.255.0")),
        ];

        IPAddress? result = LocalRtpEndpointResolver.PickSameSubnetAddress(remoteAddress, localAddresses);

        Assert.Equal(IPAddress.Parse("192.168.44.10"), result);
    }

    [Fact]
    public void PickSameSubnetAddress_returns_null_when_no_local_address_matches()
    {
        IPAddress remoteAddress = IPAddress.Parse("192.168.44.23");
        LocalRtpEndpointResolver.LocalIPv4Address[] localAddresses =
        [
            new(IPAddress.Parse("10.0.0.5"), IPAddress.Parse("255.255.255.0")),
            new(IPAddress.Parse("172.16.0.5"), IPAddress.Parse("255.255.0.0")),
        ];

        IPAddress? result = LocalRtpEndpointResolver.PickSameSubnetAddress(remoteAddress, localAddresses);

        Assert.Null(result);
    }
}
