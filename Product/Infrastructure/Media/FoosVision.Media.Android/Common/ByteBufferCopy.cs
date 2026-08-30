// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Java.Nio;

namespace FoosVision.Media.Android.Common;

public static class ByteBufferCopy
{
    public static void Put(ByteBuffer destination, ReadOnlySpan<byte> source)
    {
        if (source.Length > destination.Remaining())
        {
            throw new InvalidOperationException("Source buffer does not fit into the destination ByteBuffer.");
        }

        nint destinationAddress = destination.GetDirectBufferAddress();
        if (destinationAddress == 0)
        {
            destination.Put(source.ToArray());
            return;
        }

        int position = destination.Position();
        unsafe
        {
            fixed (byte* sourcePointer = source)
            {
                System.Buffer.MemoryCopy(
                    sourcePointer,
                    (void*)(destinationAddress + position),
                    destination.Remaining(),
                    source.Length);
            }
        }

        destination.Position(position + source.Length);
    }
}
