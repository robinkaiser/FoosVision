// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Windows.Decoding;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Windows.UnitTests.Decoding;

public class WindowsDecodedFrameTests
{
    [Fact]
    public void Dispose_invalidates_buffer_access()
    {
        WindowsDecodedFrame frame = new(1, 2, 3, 8, FrameByteFormat.RGBA8888, new byte[24], 24);

        frame.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = frame.Buffer);
    }
}
