// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using MessagePack;

namespace FoosVision.Protocol.Messages.Events;

public enum RecorderRuntimeMode
{
    Idle = 0,
    InstallRunning = 1,
    GameRunning = 2,
    Faulted = 3,
}

public enum RecorderStateChangeReason
{
    None = 0,
    CommandCompleted = 1,
    EndOfInput = 2,
    SourceStopped = 3,
    InternalError = 4,
}

[MessagePackObject(true)]
public record RecorderRuntimeStateChanged
{
    // Monotonic within one recorder process. A later snapshot/resync path can use this for staleness checks.
    public long Sequence { get; init; }

    public RecorderRuntimeMode Mode { get; init; }

    public Guid? ActiveSessionId { get; init; }

    public RecorderStateChangeReason Reason { get; init; }

    public string Detail { get; init; } = string.Empty;
}
