// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.UseCases.Live.Ports;

namespace FoosVision.UseCases.Training.ProcessFrame;

public record ProcessFrameRequest(Guid SessionId, Frame Frame, IFrameVisionOps Vision);

public interface IProcessFrameInputPort
{
    Task Handle(ProcessFrameRequest request, IProcessFrameOutputPort output, CancellationToken ct);
}
