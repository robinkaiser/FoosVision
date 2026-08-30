// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Text.Json.Serialization;

namespace FoosVision.Settings;

public class ViewerLiveVideoSettings
{
    [JsonRequired]
    public int PlaybackBufferMilliseconds { get; set; } = 25;

    [JsonRequired]
    public int MaxPlaybackBufferMilliseconds { get; set; } = 100;

    [JsonRequired]
    public bool DecoderLowLatency { get; set; } = true;

    [JsonRequired]
    public int UdpReceiveBufferBytes { get; set; } = 2 * 1024 * 1024;

    public static ViewerLiveVideoSettings CreateDefault()
    {
        return new ViewerLiveVideoSettings();
    }

    public void Validate()
    {
        if (PlaybackBufferMilliseconds < 0)
        {
            throw new InvalidOperationException($"{nameof(PlaybackBufferMilliseconds)} must be greater than or equal to zero.");
        }

        if (MaxPlaybackBufferMilliseconds <= PlaybackBufferMilliseconds)
        {
            throw new InvalidOperationException($"{nameof(MaxPlaybackBufferMilliseconds)} must be greater than {nameof(PlaybackBufferMilliseconds)}.");
        }

        if (MaxPlaybackBufferMilliseconds > 1000)
        {
            throw new InvalidOperationException($"{nameof(MaxPlaybackBufferMilliseconds)} must be less than or equal to 1000.");
        }

        if (UdpReceiveBufferBytes < 512 * 1024)
        {
            throw new InvalidOperationException($"{nameof(UdpReceiveBufferBytes)} must be greater than or equal to 524288.");
        }
    }
}
