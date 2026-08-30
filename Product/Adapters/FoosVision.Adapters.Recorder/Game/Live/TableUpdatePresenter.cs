// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Common.Live;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Live;
using FoosVision.UseCases.Calibration.UpdateTable;
using FoosVision.UseCases.Game.Ports;

namespace FoosVision.Adapters.Recorder.Game.Live;

public class TableUpdatePresenter : IUpdateTableOutputPort
{
    private readonly IGameSessionStore _SessionStore;
    private readonly IRecorderLiveDataPublisher _LiveDataPublisher;

    public TableUpdatePresenter(
        IGameSessionStore sessionStore,
        IRecorderLiveDataPublisher liveDataPublisher)
    {
        _SessionStore = sessionStore;
        _LiveDataPublisher = liveDataPublisher;
    }

    public Task ReportSuccess(TableConfiguration config)
    {
        if (_SessionStore.LoadActive().TryGetValue(out var session))
        {
            session.UpdateTableConfig(config);
        }

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
