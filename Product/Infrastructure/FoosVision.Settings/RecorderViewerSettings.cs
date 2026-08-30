// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Text.Json.Serialization;

namespace FoosVision.Settings;

public class RecorderViewerSettings
{
    [JsonRequired]
    public ViewerLiveVideoSettings LiveVideo { get; set; } = ViewerLiveVideoSettings.CreateDefault();

    public static RecorderViewerSettings CreateDefault()
    {
        return new RecorderViewerSettings();
    }

    public void Validate()
    {
        if (LiveVideo is null)
        {
            throw new InvalidOperationException($"{nameof(LiveVideo)} is required.");
        }

        LiveVideo.Validate();
    }
}
