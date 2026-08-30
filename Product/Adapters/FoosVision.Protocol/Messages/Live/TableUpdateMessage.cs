// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using MessagePack;

namespace FoosVision.Protocol.Messages.Live;

[MessagePackObject(true)]
public record TableUpdateMessage
{
    public bool IsSuccess { get; init; } = true;

    public string FailureReason { get; init; } = string.Empty;

    public TableConfigurationMessage TableConfiguration { get; init; } = new();
}

[MessagePackObject(true)]
public record TableConfigurationMessage
{
    public TrapeziumMessage Boundary { get; init; } = new();

    public IReadOnlyList<BarMessage> Bars { get; init; } = [];

    public IReadOnlyList<TrapeziumMessage> Occlusions { get; init; } = [];

    public uint TeamAPlayerColorArgb { get; init; }

    public uint TeamBPlayerColorArgb { get; init; }
}
