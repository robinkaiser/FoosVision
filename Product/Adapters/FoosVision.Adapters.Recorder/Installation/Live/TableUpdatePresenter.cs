// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Common.Live;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Live;
using FoosVision.UseCases.Calibration.UpdateTable;

namespace FoosVision.Adapters.Recorder.Installation.Live;

public class TableUpdatePresenter : IUpdateTableOutputPort
{
    private readonly IRecorderLiveDataPublisher _LiveDataPublisher;

    public TableUpdatePresenter(IRecorderLiveDataPublisher liveDataPublisher)
    {
        _LiveDataPublisher = liveDataPublisher;
    }

    public Task ReportSuccess(TableConfiguration config)
    {
        TableUpdateMessage update = new()
        {
            TableConfiguration = TableConfigurationMessageMapper.Map(config),
        };

        return _LiveDataPublisher.PublishTableUpdate(update);
    }

    public Task ReportFailure(string reason)
    {
        TableUpdateMessage update = new()
        {
            IsSuccess = false,
            FailureReason = reason,
        };

        return _LiveDataPublisher.PublishTableUpdate(update);
    }
}
