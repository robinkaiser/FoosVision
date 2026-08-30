// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Core.DecodedYuvFrames;

public class NullProducerYuvFrameHandle : IProducerYuvFrameHandle
{
    public static readonly NullProducerYuvFrameHandle Instance = new();

    private NullProducerYuvFrameHandle()
    {
    }

    public byte[] BufferY => [];

    public byte[] BufferU => [];

    public byte[] BufferV => [];

    public void MarkWritten(long timestampNs)
    {
    }
}
