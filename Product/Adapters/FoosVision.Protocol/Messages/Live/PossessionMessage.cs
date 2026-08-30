// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using MessagePack;

namespace FoosVision.Protocol.Messages.Live;

public enum TeamMessage
{
    A,
    B,
    None,
}

public enum PossessionAreaMessage
{
    Defense,
    FiveBar,
    ThreeBar,
    None,
}

[MessagePackObject(true)]
public record PossessionMessage
{
    public TeamMessage Team { get; init; }

    public PossessionAreaMessage Area { get; init; }

    public static PossessionMessage None { get; } = new()
    {
        Team = TeamMessage.None,
        Area = PossessionAreaMessage.None,
    };
}
