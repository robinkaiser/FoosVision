// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.UseCases.Game.Ports;

namespace FoosVision.UseCases.Game.ProcessFrame;

public record ProcessFrameRequest(Frame Frame, IFrameVisionOps Vision);

public interface IProcessFrameInputPort
{
    Task Handle(ProcessFrameRequest request, IProcessFrameOutputPort output, CancellationToken ct);
}
