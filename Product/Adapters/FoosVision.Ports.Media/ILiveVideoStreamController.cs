// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Ports.Media;

public interface ILiveVideoStreamController
{
    void PauseLiveVideoStream();

    void ResumeLiveVideoStream();
}
