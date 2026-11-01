// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;

namespace FoosVision.UseCases.Replay.RequestReturnToLive;

public record RequestReturnToLiveRequest(ReplayId ReplayId);

public interface IRequestReturnToLiveInputPort
{
    Task Handle(RequestReturnToLiveRequest request, IRequestReturnToLiveOutputPort output, CancellationToken ct);
}
