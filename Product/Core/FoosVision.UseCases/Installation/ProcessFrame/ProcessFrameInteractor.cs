// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Installation.Entities;
using FoosVision.UseCases.Installation.Ports;

namespace FoosVision.UseCases.Installation.ProcessFrame;

public class ProcessFrameInteractor : IProcessFrameInputPort
{
    private readonly IInstallSessionStore _SessionStore;

    public ProcessFrameInteractor(IInstallSessionStore sessionStore)
    {
        _SessionStore = sessionStore;
    }

    public async Task Handle(ProcessFrameRequest request, IProcessFrameOutputPort output, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out InstallSession session))
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
