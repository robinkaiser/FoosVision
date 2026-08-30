// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.UseCases.Calibration.Ports;

namespace FoosVision.UseCases.Calibration.UpdateTableScene;

public class UpdateTableSceneInteractor : IUpdateTableSceneInputPort
{
    private readonly ITableConfigStore _ConfigStore;

    public UpdateTableSceneInteractor(ITableConfigStore store)
    {
        _ConfigStore = store;
    }

    public async Task Handle(UpdateTableSceneRequest request, IUpdateTableSceneOutputPort output, CancellationToken ct)
    {
        if (!_ConfigStore.LoadTableConfig().TryGetValue(out TableConfiguration tableConfig))
        {
            await output.ReportFailure("No table configuration available.");
            return;
        }

        var ballPosition = request.BallPosition;
        var vision = request.Vision;

        vision.Update(tableConfig, ballPosition);

        await output.ReportSuccess();
    }
}
