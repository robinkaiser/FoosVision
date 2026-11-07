// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Replay.ValueObjects;

namespace FoosVision.Adapters.Recorder.Replay;

public interface IReplayCoordinator
{
    Task RequestReplay(
        Guid sessionId,
        Frame triggerFrame,
        ReplayPlan replay);

    void StartSession(Guid sessionId);

    void StopSession(Guid sessionId);

    Task RequestReturnToLive(Guid sessionId, Frame triggerFrame);
}
