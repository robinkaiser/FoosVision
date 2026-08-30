// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Entities;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.UseCases.Replay.Ports;

namespace FoosVision.UseCases.Replay.ProcessReplayFrame;

public class ProcessReplayFrameInteractor : IProcessReplayFrameInputPort
{
    private readonly IReplaySessionStore _SessionStore;

    public ProcessReplayFrameInteractor(IReplaySessionStore sessionStore)
    {
        _SessionStore = sessionStore;
    }

    public async Task Handle(ProcessReplayFrameRequest request, IProcessReplayFrameOutputPort output, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out ReplaySession session))
        {
            await output.ReportSkipped("No active session.");
            return;
        }

        if (!session.CurrentReplayId.TryGetValue(out ReplayId replayId))
        {
            await output.ReportSkipped("No active replay.");
            return;
        }

        if (!session.TableConfiguration.TryGetValue(out TableConfiguration tableConfiguration))
        {
            await output.ReportSkipped("Replay has no table configuration.");
            return;
        }

        if (!session.CanApplyObservations(request.Frame))
        {
            await output.ReportSkipped("Replay frame is not after the track anchor.");
            return;
        }

        Rectangle regionOfInterest = session.GetBallSearchRegion();
        var observations = request.Vision.DetectBalls(tableConfiguration, regionOfInterest);
        session.ApplyObservations(request.Frame, observations);

        ReplayFrameProcessedResponse response = new(replayId);

        await output.ReportReplayFrameProcessed(response);
    }
}
