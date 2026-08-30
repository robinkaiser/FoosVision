// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.UseCases.Calibration.Ports;

namespace FoosVision.UseCases.Calibration.UpdateTable;

public class UpdateTableInteractor : IUpdateTableInputPort
{
    private readonly ITableConfigStore _ConfigStore;

    public UpdateTableInteractor(ITableConfigStore store)
    {
        _ConfigStore = store;
    }

    public async Task Handle(UpdateTableRequest request, IUpdateTableOutputPort output, CancellationToken ct)
    {
        var mode = request.Mode;
        var vision = request.Vision;

        switch (mode)
        {
            case UpdateMode.Reset:
                _ConfigStore.ClearTableConfig();
                break;

            case UpdateMode.Update:
                if (!_ConfigStore.LoadTableConfig().TryGetValue(out _))
                {
                    await output.ReportFailure("No table configuration available.");
                    return;
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(request));
        }

        var config = vision.Detect();

        if (config.IsNone)
        {
            await output.ReportFailure("Detect table configuration failed.");
            return;
        }

        _ConfigStore.SaveTableConfig(config.Value);

        await output.ReportSuccess(config.Value);
    }
}
