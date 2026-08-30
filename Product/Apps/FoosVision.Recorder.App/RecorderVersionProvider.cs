// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Connectivity;

namespace FoosVision.Recorder.App;

public class RecorderVersionProvider : IRecorderVersionProvider
{
    public string GetAppVersion()
    {
        if (!string.IsNullOrWhiteSpace(AppInfo.Current.VersionString))
        {
            return AppInfo.Current.VersionString;
        }

        return "0.0.0";
    }
}
