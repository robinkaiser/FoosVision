// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Ports.Media;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;

internal sealed class TestYuvFrameHandle : IYuvFrameHandle
{
    public TestYuvFrameHandle(Frame meta, YuvFrameLayout layout)
    {
        Meta = meta;
        Layout = layout;
        BufferY = [128];
        BufferU = [128];
        BufferV = [128];
    }

    public Frame Meta { get; }

    public YuvFrameLayout Layout { get; }

    public byte[] BufferY { get; }

    public byte[] BufferU { get; }

    public byte[] BufferV { get; }

    public bool IsReleased { get; private set; }

    public void Release()
    {
        IsReleased = true;
    }
}
