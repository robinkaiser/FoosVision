// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Setup.Entities;
using FoosVision.UseCases.Setup.Ports;

namespace FoosVision.UseCases.Setup.ProcessFrame;

public class ProcessFrameInteractor : IProcessFrameInputPort
{
    private readonly ISetupSessionStore _SessionStore;

    public ProcessFrameInteractor(ISetupSessionStore sessionStore)
    {
        _SessionStore = sessionStore;
    }

    public async Task Handle(ProcessFrameRequest request, IProcessFrameOutputPort output, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out SetupSession session))
        {
            await output.ReportSkipped("No active session.");
            return;
        }

        var changes = session.ApplyFrame(request.Frame);
        bool requestTableUpdate = false;

        foreach (var change in changes)
        {
            switch (change)
            {
                case UpdateTableConfigRequest:
                    requestTableUpdate = true;
                    break;
            }
        }

        ProcessFrameResponse response = new(
            request.Frame,
            requestTableUpdate);

        await output.ReportProcessed(response);
    }
}
