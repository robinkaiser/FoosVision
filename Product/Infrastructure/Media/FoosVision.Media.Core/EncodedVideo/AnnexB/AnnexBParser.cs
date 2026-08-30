// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Core.EncodedVideo.AnnexB;

public static class AnnexBParser
{
    private const int _MinHeaderSize = 4;

    public static bool TryFindStartCode(
        byte[] buffer,
        int startOffset,
        int endOffsetExclusive,
        out int startCodeOffset,
        out int headerOffset)
    {
        if (startOffset >= endOffsetExclusive ||
            (endOffsetExclusive - startOffset) < _MinHeaderSize)
        {
            startCodeOffset = -1;
            headerOffset = -1;
            return false;
        }

        int i = startOffset;

        do
        {
            if (buffer[i + 0] != 0x0 ||
                buffer[i + 1] != 0x0)
            {
                i++;
                continue;
            }

            if (buffer[i + 2] == 0x1)
            {   // 3-Byte-Startcode: 00 00 01
                startCodeOffset = i;
                headerOffset = i + 3;
                return true;
            }

            if (buffer[i + 2] == 0x0 &&
                buffer[i + 3] == 0x1)
            {   // 4-Byte-Startcode: 00 00 00 01
                startCodeOffset = i;
                headerOffset = i + 4;
                return true;
            }

            i++;
        }
        while (i < endOffsetExclusive - _MinHeaderSize);

        startCodeOffset = -1;
        headerOffset = -1;
        return false;
    }

    public static int FindNals(
        byte[] buffer,
        int startOffset,
        int endOffsetExclusive,
        AnnexBNalRange[] outNalBuffer,
        int maxOutNalCount)
    {
        if (startOffset >= endOffsetExclusive)
        {
            return 0;
        }

        if (!TryFindStartCode(buffer, startOffset, endOffsetExclusive, out int currentStartCodeOffset, out int currentHeaderOffset))
        {
            return 0;
        }

        int count = 0;

        while (true)
        {
            bool hasNext = TryFindStartCode(
                buffer,
                currentHeaderOffset,
                endOffsetExclusive,
                out int nextStartCodeOffset,
                out int nextHeaderOffset);

            int currentNalEndOffsetExclusive = hasNext
                ? nextStartCodeOffset
                : endOffsetExclusive;

            if (currentHeaderOffset < currentNalEndOffsetExclusive)
            {
                outNalBuffer[count].StartOffset = currentStartCodeOffset;
                outNalBuffer[count].HeaderOffset = currentHeaderOffset;
                outNalBuffer[count].EndOffsetExclusive = currentNalEndOffsetExclusive;
                count++;
            }

            if (!hasNext) break;
            if (count == maxOutNalCount)
            {
                return -1;
            }

            currentStartCodeOffset = nextStartCodeOffset;
            currentHeaderOffset = nextHeaderOffset;
        }

        return count;
    }
}
