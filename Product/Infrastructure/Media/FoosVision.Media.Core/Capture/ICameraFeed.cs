// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.DecodedFrames;
using FoosVision.Media.Core.EncodedVideo;

namespace FoosVision.Media.Core.Capture;

public interface ICameraFeed
{
    Task<bool> Configure();

    Task<bool> Start(IFrameSink frameSink, IEncodedAccessUnitSink encodedUnitSink);

    Task Stop();
}
