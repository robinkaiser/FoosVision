// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Protocol.Connectivity.Configuration;

public static class DefaultPorts
{
    public const int HandshakeReqRepTcp = 5555;
    public const int CommandsReqRepTcp = 5556;
    public const int EventsPubSubTcp = 5557;
    public const int LiveDataPubSubTcp = 5558;
    public const int LiveAnalysisPubSubTcp = 5559;

    public const int DiscoveryUdp = 5560;
    public const int RtpH264StreamUdp = 5561;
}
