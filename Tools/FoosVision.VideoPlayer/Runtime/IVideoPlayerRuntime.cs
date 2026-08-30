// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.VideoPlayer.Runtime;

public interface IVideoPlayerRuntime : IDisposable
{
    void Start();
}
