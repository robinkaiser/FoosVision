// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Training.Entities;
using FoosVision.UseCases.Common;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Training.Ports;

namespace FoosVision.UseCases.Training.StopTraining;

public class StopTrainingInteractor : IStopTrainingInputPort
{
    private readonly ITrainingSessionStore _SessionStore;
    private readonly IFrameSource _FrameSource;
    private readonly SessionGate _SessionGate;

    public StopTrainingInteractor(
        ITrainingSessionStore sessionStore,
        IFrameSource frameSource,
        SessionGate sessionGate)
    {
        _SessionStore = sessionStore;
        _FrameSource = frameSource;
        _SessionGate = sessionGate;
    }

    public async Task Handle(StopTrainingRequest request, IStopTrainingOutputPort output, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out TrainingSession session))
        {
            await output.ReportStopFailed("No active session.");
            return;
        }

        var sessionId = session.Id;

        session.Stop();

        try
        {
            await _FrameSource.Stop(ct);
        }
        finally
        {
            _SessionStore.Clear();

            try
            {
                await output.ReportStopped(new StopTrainingResponse(sessionId));
            }
            finally
            {
                _SessionGate.Release(sessionId);
            }
        }
    }
}
