// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Common.Logging;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Protocol.Messages.Live;

namespace FoosVision.Adapters.Viewer.Session.Active;

internal class TableUpdatePresenter
{
    private static readonly Source _Log = new("Viewer.Session.Active.TableUpdatePresenter");

    private readonly IOverlaySink _OverlaySink;
    private readonly Action<TableConfiguration> _UpdateTrackingTableConfiguration;
    private readonly Action _ResetTrackingProjection;
    private readonly Action _RefreshUiState;

    public TableUpdatePresenter(
        IOverlaySink overlaySink,
        Action<TableConfiguration> updateTrackingTableConfiguration,
        Action resetTrackingProjection,
        Action refreshUiState)
    {
        _OverlaySink = overlaySink;
        _UpdateTrackingTableConfiguration = updateTrackingTableConfiguration;
        _ResetTrackingProjection = resetTrackingProjection;
        _RefreshUiState = refreshUiState;
    }

    public Option<TableConfiguration> LatestTableConfiguration { get; private set; } = Option<TableConfiguration>.None();

    public void Handle(TableUpdateMessage message)
    {
        if (!message.IsSuccess)
        {
            _Log.Information("Table update failed. Reason={0}", message.FailureReason);
            LatestTableConfiguration = Option<TableConfiguration>.None();
            _ResetTrackingProjection();
            _OverlaySink.UpdateTableState(TableOverlayState.Empty);
            _RefreshUiState();
            return;
        }

        if (TableConfigurationMessageMapper.TryMap(message.TableConfiguration, out TableConfiguration tableConfiguration))
        {
            LatestTableConfiguration = Option<TableConfiguration>.Some(tableConfiguration);
            _UpdateTrackingTableConfiguration(tableConfiguration);
            _RefreshUiState();
        }
        else
        {
            _Log.Warning("Table update ignored because it could not be mapped to a table configuration.");
        }

        TableOverlayState state = TableOverlayStateMapper.Map(message);
        _OverlaySink.UpdateTableState(state);
    }
}
