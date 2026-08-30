// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.VideoPlayer.Options;

namespace FoosVision.VideoPlayer.Runtime;

public class VideoPlayerRuntimeFactory : IVideoPlayerRuntimeFactory
{
    public IVideoPlayerRuntime Create(VideoPlayerOptions options)
    {
        return new VideoPlayerRuntime(options);
    }
}
