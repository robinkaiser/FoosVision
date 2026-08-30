// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Core.EncodedVideoStreaming;

public interface IEncodedVideoStreamSinkFactory
{
    IEncodedVideoStreamSink Create();
}
