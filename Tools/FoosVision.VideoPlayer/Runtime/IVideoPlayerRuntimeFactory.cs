// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.VideoPlayer.Options;

namespace FoosVision.VideoPlayer.Runtime;

public interface IVideoPlayerRuntimeFactory
{
    IVideoPlayerRuntime Create(VideoPlayerOptions options);
}
