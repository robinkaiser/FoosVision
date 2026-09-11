// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using MessagePack;

namespace FoosVision.Protocol.Messages.Live;

[MessagePackObject(true)]
public record ProcessFrameRateMessage
{
    public double? FramesPerSecond { get; init; }
}
