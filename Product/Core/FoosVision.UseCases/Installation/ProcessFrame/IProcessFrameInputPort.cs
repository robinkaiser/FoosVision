// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.UseCases.Installation.ProcessFrame;

public record ProcessFrameRequest(Frame Frame);

public interface IProcessFrameInputPort
{
    Task Handle(ProcessFrameRequest request, IProcessFrameOutputPort output, CancellationToken ct);
}
