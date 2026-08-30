// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Core.DecodedFrames;

public class NullProducerFrameHandle : IProducerFrameHandle
{
    public static readonly NullProducerFrameHandle Instance = new();

    private NullProducerFrameHandle()
    {
    }

    public byte[] BufferRGBA8888 => [];

    public void MarkWritten(long timestampNs)
    {
    }
}
