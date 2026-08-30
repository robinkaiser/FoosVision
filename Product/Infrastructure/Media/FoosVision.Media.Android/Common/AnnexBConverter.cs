// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Runtime.InteropServices;
using Android.Media;
using Java.Nio;

namespace FoosVision.Media.Android.Common;

public static class AnnexBConverter
{
    private const int _StartCodeSize = 4;
    private static readonly byte[] _StartCode4 = { 0x00, 0x00, 0x00, 0x01 };

    // Converts a MediaCodec output access unit into Annex-B (start-code-prefixed) NAL units.
    // If the buffer already looks like Annex-B, it is copied as-is.
    // Returns number of bytes written.
    public static int WriteAccessUnit(ByteBuffer buffer, MediaCodec.BufferInfo info, byte[] dst, int dstOffset)
    {
        int offset = info.Offset;
        int size = info.Size;

        if (size <= 0) return 0;
        if (dstOffset < 0 || dstOffset >= dst.Length) return 0;

        int dstRemaining = dst.Length - dstOffset;

        if (LooksLikeAnnexB(buffer, offset, size))
        {
            if (size > dstRemaining) return 0;
            CopyBytesToArray(buffer, offset, dst, dstOffset, size);
            return size;
        }

        int end = offset + size;
        int outPos = dstOffset;

        while (offset + 4 <= end)
        {
            int nalLen = ReadInt32BE(buffer, offset);
            offset += 4;

            if (nalLen <= 0 || offset + nalLen > end)
                break;

            int required = _StartCodeSize + nalLen;
            if ((outPos - dstOffset) + required > dstRemaining)
                break;

            _StartCode4.CopyTo(dst, outPos);
            outPos += _StartCodeSize;

            CopyBytesToArray(buffer, offset, dst, outPos, nalLen);
            outPos += nalLen;

            offset += nalLen;
        }

        return outPos - dstOffset;
    }

    // Writes codec specific data (CSD) buffers in Annex-B form.
    // Returns bytes written.
    public static int WriteCsd(ByteBuffer csd, byte[] dst, int dstOffset)
    {
        int len = csd.Remaining();
        if (len <= 0) return 0;

        int pos = csd.Position();
        int dstRemaining = dst.Length - dstOffset;

        if (dstOffset < 0 || dstOffset >= dst.Length) return 0;

        // Already Annex-B => copy as-is
        if (LooksLikeAnnexB(csd, pos, len))
        {
            if (len > dstRemaining) return 0;
            CopyBytesToArray(csd, pos, dst, dstOffset, len);
            return len;
        }

        // Length-prefixed (AVCC/HVCC style) => convert like a normal access unit
        if (LooksLikeLengthPrefixed(csd, pos, len))
        {
            var info = new MediaCodec.BufferInfo();
            info.Set(pos, len, 0, MediaCodecBufferFlags.None);
            return WriteAccessUnit(csd, info, dst, dstOffset);
        }

        // Otherwise: treat as a single raw NAL without start code
        if (_StartCodeSize + len > dstRemaining) return 0;

        _StartCode4.CopyTo(dst, dstOffset);
        CopyBytesToArray(csd, pos, dst, dstOffset + _StartCodeSize, len);
        return _StartCodeSize + len;
    }

    private static bool LooksLikeAnnexB(ByteBuffer buffer, int offset, int size)
    {
        if (size < 4) return false;

        byte b0 = (byte)buffer.Get(offset + 0);
        byte b1 = (byte)buffer.Get(offset + 1);
        byte b2 = (byte)buffer.Get(offset + 2);
        byte b3 = (byte)buffer.Get(offset + 3);

        return (b0 == 0x00 && b1 == 0x00 && b2 == 0x01) ||
               (b0 == 0x00 && b1 == 0x00 && b2 == 0x00 && b3 == 0x01);
    }

    private static bool LooksLikeLengthPrefixed(ByteBuffer buffer, int offset, int size)
    {
        if (size < 4) return false;

        int nalLen = ReadInt32BE(buffer, offset);

        // must fit in remaining payload after the 4-byte length
        return nalLen > 0 && nalLen <= (size - 4);
    }

    private static int ReadInt32BE(ByteBuffer buffer, int offset)
    {
        int b0 = (byte)buffer.Get(offset + 0);
        int b1 = (byte)buffer.Get(offset + 1);
        int b2 = (byte)buffer.Get(offset + 2);
        int b3 = (byte)buffer.Get(offset + 3);

        return (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
    }

    private static void CopyBytesToArray(ByteBuffer buffer, int srcOffset, byte[] dst, int dstOffset, int count)
    {
        nint ptr = buffer.GetDirectBufferAddress();

        if (ptr != 0)
        {
            Marshal.Copy(ptr + srcOffset, dst, dstOffset, count);
            return;
        }

        int oldPos = buffer.Position();
        buffer.Position(srcOffset);
        buffer.Get(dst, dstOffset, count);
        buffer.Position(oldPos);
    }
}
