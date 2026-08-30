// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Capture.ValueObjects;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.UseCases.Calibration.Ports;
using FoosVision.UseCases.Dependencies.Settings;

namespace FoosVision.Recorder.Composition.InMemoryStores;

internal class SettingsStore : ISettingsStore, ITableConfigStore
{
    private readonly Lock _Gate = new();

    private readonly CameraProfile _CameraProfile = new(
        CameraType.Main,
        CameraFieldOfView.FullTableFromAbove,
        CameraResolution.FullHD,
        ProcessingFps: 30,
        HighFps: 120);

    private Option<TableConfiguration> _TableConfig = Option<TableConfiguration>.None();

    public Option<TableConfiguration> LoadTableConfig()
    {
        lock (_Gate) return _TableConfig;
    }

    public CameraProfile LoadCameraProfile()
    {
        lock (_Gate) return _CameraProfile;
    }

    public void SaveTableConfig(TableConfiguration config)
    {
        lock (_Gate) _TableConfig = config;
    }

    public void ClearTableConfig()
    {
        lock (_Gate) _TableConfig = Option<TableConfiguration>.None();
    }
}
