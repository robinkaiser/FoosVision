// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using MessagePack;

namespace FoosVision.Protocol.Messages.Handshake;

[MessagePackObject(true)]
public record HandshakeDiagnosticsSettings
{
    public HandshakeSeqLoggingSettings Seq { get; init; } = new();

    public HandshakeRuntimeMetricsSettings RuntimeMetrics { get; init; } = new();
}

[MessagePackObject(true)]
public record HandshakeSeqLoggingSettings
{
    public bool Enabled { get; init; }

    public string ServerUrl { get; init; } = string.Empty;

    public string MinimumLevel { get; init; } = string.Empty;

    public bool SendTestEventOnStartup { get; init; }
}

[MessagePackObject(true)]
public record HandshakeRuntimeMetricsSettings
{
    public bool Enabled { get; init; }

    public int ReportIntervalSeconds { get; init; } = 10;
}
