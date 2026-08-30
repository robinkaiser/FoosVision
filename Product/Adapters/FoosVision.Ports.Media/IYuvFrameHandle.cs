// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Ports.Media;

public interface IYuvFrameHandle
{
    Frame Meta { get; }

    YuvFrameLayout Layout { get; }

    byte[] BufferY { get; }

    byte[] BufferU { get; }

    byte[] BufferV { get; }

    void Release();
}
