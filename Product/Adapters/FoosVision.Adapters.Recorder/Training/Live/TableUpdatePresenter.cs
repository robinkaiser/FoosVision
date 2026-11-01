// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Common.Live;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.Training.Entities;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Live;
using FoosVision.UseCases.Calibration.UpdateTable;

namespace FoosVision.Adapters.Recorder.Training.Live;

public class TableUpdatePresenter : IUpdateTableOutputPort
{
    private readonly TrainingSession _Session;
    private readonly IRecorderLiveDataPublisher _LiveDataPublisher;

    public TableUpdatePresenter(
        TrainingSession session,
        IRecorderLiveDataPublisher liveDataPublisher)
    {
        _Session = session;
        _LiveDataPublisher = liveDataPublisher;
    }

    public Task ReportSuccess(TableConfiguration config)
    {
        _Session.UpdateTableConfig(config);

        TableUpdateMessage update = new()
        {
            TableConfiguration = TableConfigurationMessageMapper.Map(config),
        };

        return _LiveDataPublisher.PublishTableUpdate(update);
    }

    public Task ReportFailure(string reason)
    {
        return Task.CompletedTask;
    }
}
