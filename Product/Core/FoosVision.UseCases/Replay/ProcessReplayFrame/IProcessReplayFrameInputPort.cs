// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.UseCases.Replay.Ports;

namespace FoosVision.UseCases.Replay.ProcessReplayFrame;

public record ProcessReplayFrameRequest(Frame Frame, IReplayVisionOps Vision);

public interface IProcessReplayFrameInputPort
{
    Task Handle(ProcessReplayFrameRequest request, IProcessReplayFrameOutputPort output, CancellationToken ct);
}
