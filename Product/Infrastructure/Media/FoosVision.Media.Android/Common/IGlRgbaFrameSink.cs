// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Android.Common;

internal interface IGlRgbaFrameSink
{
    void OnFrameAvailable(long timestampNs, nint bufferAddress, int bufferLength);
}
