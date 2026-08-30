// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;

namespace FoosVision.UseCases.Replay.ObserveLiveTracking;

public record ReturnToLiveResponse(ReplayId ReplayId);

public interface IObserveLiveTrackingOutputPort
{
    Task ReportReturnToLive(ReturnToLiveResponse response);
}
