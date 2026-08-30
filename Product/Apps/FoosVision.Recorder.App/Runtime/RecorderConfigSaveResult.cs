// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Recorder.App.Runtime;

public record RecorderConfigSaveResult(
    RecorderConfigSaveStatus Status,
    string? Error)
{
    public static RecorderConfigSaveResult Saved()
    {
        return new RecorderConfigSaveResult(RecorderConfigSaveStatus.Saved, null);
    }

    public static RecorderConfigSaveResult Invalid(string error)
    {
        return new RecorderConfigSaveResult(RecorderConfigSaveStatus.InvalidConfig, error);
    }

    public static RecorderConfigSaveResult Failed(string error)
    {
        return new RecorderConfigSaveResult(RecorderConfigSaveStatus.SaveFailed, error);
    }
}
