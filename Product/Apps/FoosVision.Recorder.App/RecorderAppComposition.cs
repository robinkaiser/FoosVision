// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Recorder.App;

/// <summary>
/// Exposes recorder UI composition for application hosts.
/// </summary>
public static class RecorderAppComposition
{
    public static MainPage CreateRecorderPage()
    {
        return RecorderPlatformComposition.CreateMainPage();
    }
}
