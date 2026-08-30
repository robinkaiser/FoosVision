// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Runtime.CompilerServices;
using FoosVision.Common.Types;

namespace FoosVision.Vision.BallFinding.Processing;

public enum SelfCheckStatus
{
    OkPoolFullyFree = 0,
    OkPoolInUse = 1,
    ErrorFreeTopOutOfRange = 2,
}

public record class BlobFinderParameters
{
    public int MinBlobSize { get; init; } = 32;

    public int MinExtendX { get; init; } = 5;

    public int MinExtendY { get; init; } = 5;

    public int MaxBlobCount { get; init; } = 100;

    public static readonly BlobFinderParameters Default = new();
}

public record struct Blob
{
    public Blob(int pixelCount, int boundsX0, int boundsY0, int boundsX1, int boundsY1)
    {
        PixelCount = pixelCount;
        BoundsX0 = boundsX0;
        BoundsY0 = boundsY0;
        BoundsX1 = boundsX1;
        BoundsY1 = boundsY1;
    }

    public int PixelCount { get; set; }
    public int BoundsX0 { get; set; }
    public int BoundsY0 { get; set; }
    public int BoundsX1 { get; set; }
    public int BoundsY1 { get; set; }
}

public unsafe class BlobFinder
{
    private struct InternalBlob
    {
        public int Id;

        // DSU
        public int ParentId;
        public int Size;

        // Streaming lifetime
        public int ActiveRuns;

        // Stats (valid at root)
        public int Area;
        public int X0;
        public int Y0;
        public int X1;
        public int Y1;
    }

    private struct Segment
    {
        public int BlobId; // Root at row end
        public int Start;
        public int EndExclusive;
    }

    private readonly int _ImageWidth;
    private readonly int _Segment_MaxCount;
    private readonly int _ActiveBlob_MaxCount;

    private readonly int _MinBlobSize;
    private readonly int _MinBlobExtendX;
    private readonly int _MinBlobExtendY;
    private readonly int _MaxBlobCount;

    private readonly byte _Threshold;

    private readonly Segment[] _Segments0;
    private readonly Segment[] _Segments1;

    private readonly InternalBlob[] _InternalBlobs;
    private readonly Blob[] _ResultBlobs;

    private readonly int[] _FreeBlobIds;
    private readonly int[] _UnionLosers;

    private int _FreeTop;
    private int _UnionLoserCount;

    private Rectangle _ProcessingRectangle;
    private int _CurrentRowNumber;
    private int _BlobCount;
    private InternalBlob* _Blobs;

    public BlobFinder(int imageWidth, BlobFinderParameters parameters)
    {
        _ImageWidth = imageWidth;
        _Segment_MaxCount = ((imageWidth + 1) / 2) + 1;
        _ActiveBlob_MaxCount = ((imageWidth + 1) / 2) + 2;

        _MinBlobSize = parameters.MinBlobSize;
        _MinBlobExtendX = parameters.MinExtendX;
        _MinBlobExtendY = parameters.MinExtendY;
        _MaxBlobCount = parameters.MaxBlobCount;

        _Threshold = 0;

        _Segments0 = new Segment[_Segment_MaxCount];
        _Segments1 = new Segment[_Segment_MaxCount];

        _InternalBlobs = new InternalBlob[_ActiveBlob_MaxCount];
        for (int i = 0; i < _ActiveBlob_MaxCount; i++)
        {
            _InternalBlobs[i].Id = i;
            _InternalBlobs[i].ParentId = i;
            _InternalBlobs[i].Size = 1;
        }

        _FreeBlobIds = new int[_ActiveBlob_MaxCount];
        for (int i = 0; i < _ActiveBlob_MaxCount; i++)
        {
            _FreeBlobIds[i] = i;
        }
        _FreeTop = _ActiveBlob_MaxCount;

        _UnionLosers = new int[_ActiveBlob_MaxCount];
        _ResultBlobs = new Blob[_MaxBlobCount];
    }

    public Blob[] ResultBlobBuffer => _ResultBlobs;

    public int ProcessY8(byte[] inputY8, Rectangle rect)
    {
        _ProcessingRectangle = rect;
        _FreeTop = _ActiveBlob_MaxCount;

        for (int i = 0; i < _ActiveBlob_MaxCount; i++)
        {
            _FreeBlobIds[i] = i;

            _InternalBlobs[i].ParentId = i;
            _InternalBlobs[i].Size = 1;
            _InternalBlobs[i].ActiveRuns = 0;
            _InternalBlobs[i].Area = 0;
        }

        fixed (InternalBlob* pBlobs = _InternalBlobs)
        fixed (Segment* pSeg0 = _Segments0)
        fixed (Segment* pSeg1 = _Segments1)
        fixed (byte* pInY8 = inputY8)
        {
            _Blobs = pBlobs;
            return ProcessY8(pSeg0, pSeg1, pInY8);
        }
    }

    public SelfCheckStatus SelfCheck()
    {
        if (_FreeTop < 0 || _FreeTop > _ActiveBlob_MaxCount)
        {
            return SelfCheckStatus.ErrorFreeTopOutOfRange;
        }

        if (_FreeTop == _ActiveBlob_MaxCount)
        {
            return SelfCheckStatus.OkPoolFullyFree;
        }

        return SelfCheckStatus.OkPoolInUse;
    }

    private int ProcessY8(Segment* pSeg0, Segment* pSeg1, byte* pInY8)
    {
        var rect = _ProcessingRectangle;
        int startX = rect.X;
        int startY = rect.Y;

        int width = rect.Width;
        int height = rect.Height;

        int stride = _ImageWidth;
        byte* pSrc = pInY8 + (stride * startY) + startX;

        _BlobCount = 0;
        _CurrentRowNumber = 0;

        Segment* pPrev = pSeg1;
        Segment* pCur = pSeg0;
        int prevCount = 0;

        for (_CurrentRowNumber = 0; _CurrentRowNumber < height; _CurrentRowNumber++)
        {
            _UnionLoserCount = 0;
            int curCount = ProcessRow(pSrc, width, pCur, pPrev, prevCount);
            pSrc += stride;

            if (_UnionLoserCount != 0)
            {
                CanonicalizeSegments(pCur, curCount);
                RecycleUnionLosers();
            }

            // Swap prev/cur
            var pTemp = pCur;
            pCur = pPrev;
            pPrev = pTemp;

            prevCount = curCount;
        }

        // Flush: no more rows-> all Prev segments will be retired
        _UnionLoserCount = 0;
        RetireRemainingPrevSegments(pPrev, prevCount);
        RecycleUnionLosers();

        return _BlobCount;
    }

    private int ProcessRow(byte* pRowStart, int width, Segment* pCur, Segment* pPrev, int prevCount)
    {
        int curCount = 0;
        int prevIdx = 0;
        int col = 0;

        while (col < width)
        {
            // In contrast to while, measurably more JIT-friendly
            for (; col < width; col++)
            {   // Skip background
                if (pRowStart[col] > _Threshold) break;
            }

            if (col >= width) break;

            int start = col;

            for (; col < width; col++)
            {    // Foreground run
                if (pRowStart[col] <= _Threshold) break;
            }

            int endExclusive = col;

            while (prevIdx < prevCount && PrevSegmentIsBeforeCurrent8(start, pPrev[prevIdx].EndExclusive))
            {   // Retire prev segments strictly left of this run (cannot touch anything anymore)
                RetirePrevSegment(pPrev[prevIdx].BlobId);
                prevIdx++;
            }

            // Collect overlaps/touches with remaining prev segments
            int root = -1;
            int j = prevIdx;

            while (j < prevCount && SegmentsConnectOrOverlap8(endExclusive, pPrev[j].Start))
            {   // Connect with previous segment
                int rj = FindRoot(pPrev[j].BlobId);

                if (root < 0)
                {
                    root = rj;
                }
                else
                {
                    root = Union(root, rj);
                }

                // Rewrite prev label to current root to reduce later Find costs
                pPrev[j].BlobId = root;
                j++;
            }

            if (root < 0)
            {   // New component
                root = AllocNewRoot(start, endExclusive);
            }

            // Add current run stats to root
            AddRunToRoot(root, start, endExclusive, _CurrentRowNumber);

            // Current run contributes to active set for next row
            (_Blobs + root)->ActiveRuns++;

            // Emit current segment
            // Segment buffer overflow should be impossible
            pCur[curCount].Start = start;
            pCur[curCount].EndExclusive = endExclusive;
            pCur[curCount].BlobId = root;
            curCount++;
        }

        while (prevIdx < prevCount)
        {   // Retire all remaining prev segments (right side)
            RetirePrevSegment(pPrev[prevIdx].BlobId);
            prevIdx++;
        }

        return curCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RetireRemainingPrevSegments(Segment* pPrev, int prevCount)
    {
        for (int i = 0; i < prevCount; i++)
        {
            RetirePrevSegment(pPrev[i].BlobId);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RetirePrevSegment(int blobId)
    {
        int root = FindRoot(blobId);

        InternalBlob* pRoot = _Blobs + root;

        pRoot->ActiveRuns--;

        if (pRoot->ActiveRuns == 0)
        {
            FinalizeAndRecycleRoot(root);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FinalizeAndRecycleRoot(int rootId)
    {
        InternalBlob* p = _Blobs + rootId;

        int size = p->Area;
        if (size != 0)
        {
            int extX = p->X1 - p->X0 + 1;
            int extY = p->Y1 - p->Y0 + 1;

            if ((_BlobCount < _MaxBlobCount) &&
                (size >= _MinBlobSize) &&
                (extX >= _MinBlobExtendX) &&
                (extY >= _MinBlobExtendY))
            {
                var rect = _ProcessingRectangle;

                _ResultBlobs[_BlobCount].PixelCount = size;
                _ResultBlobs[_BlobCount].BoundsX0 = p->X0 + rect.X;
                _ResultBlobs[_BlobCount].BoundsY0 = p->Y0 + rect.Y;
                _ResultBlobs[_BlobCount].BoundsX1 = p->X1 + rect.X;
                _ResultBlobs[_BlobCount].BoundsY1 = p->Y1 + rect.Y;

                _BlobCount++;
            }
        }

        // Recycle root
        //ResetNode(rootId);
        PushFreeBlobId(rootId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddRunToRoot(int rootId, int start, int end, int y)
    {
        InternalBlob* p = _Blobs + rootId;

        int len = end - start;
        p->Area += len;

        if (start < p->X0) p->X0 = start;
        if (end - 1 > p->X1) p->X1 = end - 1;
        p->Y1 = y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AllocNewRoot(int start, int end)
    {
        int id = PopFreeBlobId();
        InternalBlob* p = _Blobs + id;

        // DSU init
        p->ParentId = id;
        p->Size = 1;

        // Lifetime
        p->ActiveRuns = 0;

        // Stats init
        p->Area = 0;
        p->X0 = start;
        p->X1 = end - 1;
        p->Y0 = _CurrentRowNumber;
        p->Y1 = _CurrentRowNumber;

        return id;
    }

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //private void ResetNode(int id)
    //{
    //    InternalBlob* p = _Blobs + id;

    //    p->ParentId = id;
    //    p->Size = 1;
    //    p->ActiveRuns = 0;

    //    p->Area = 0;
    //    p->X0 = 0;
    //    p->Y0 = 0;
    //    p->X1 = 0;
    //    p->Y1 = 0;
    //}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CanonicalizeSegments(Segment* segs, int count)
    {
        for (int i = 0; i < count; i++)
        {
            segs[i].BlobId = FindRoot(segs[i].BlobId);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecycleUnionLosers()
    {
        for (int i = 0; i < _UnionLoserCount; i++)
        {
            int loser = _UnionLosers[i];

            // Loser is no root and is not referenced by any segment.
            //ResetNode(loser);
            PushFreeBlobId(loser);
        }

        _UnionLoserCount = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindRoot(int id)
    {   // Disjoint Set Union (DSU): Find
        int parent = (_Blobs + id)->ParentId;

        while (parent != id)
        {
            int grand = (_Blobs + parent)->ParentId;
            (_Blobs + id)->ParentId = grand;
            id = parent;
            parent = grand;
        }

        return id;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Union(int a, int b)
    {   // Disjoint Set Union (DSU): Union
        int ra = FindRoot(a);
        int rb = FindRoot(b);
        if (ra == rb) return ra;

        InternalBlob* pa = _Blobs + ra;
        InternalBlob* pb = _Blobs + rb;

        // Union by size
        if (pa->Size < pb->Size)
        {
            (ra, rb) = (rb, ra);

            var pt = pa;
            pa = pb;
            pb = pt;
        }

        // rb -> ra
        pb->ParentId = ra;
        pa->Size += pb->Size;

        // Merge stats into ra
        pa->Area += pb->Area;

        if (pb->X0 < pa->X0) pa->X0 = pb->X0;
        if (pb->Y0 < pa->Y0) pa->Y0 = pb->Y0;
        if (pb->X1 > pa->X1) pa->X1 = pb->X1;
        if (pb->Y1 > pa->Y1) pa->Y1 = pb->Y1;

        // Merge streaming counters
        pa->ActiveRuns += pb->ActiveRuns;
        pb->ActiveRuns = 0;

        // Defer recycling loser until end-of-row canonicalization
        _UnionLosers[_UnionLoserCount++] = rb;

        return ra;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PopFreeBlobId()
    {
        _FreeTop--;
        return _FreeBlobIds[_FreeTop];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushFreeBlobId(int id)
    {
        _FreeBlobIds[_FreeTop] = id;
        _FreeTop++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PrevSegmentIsBeforeCurrent8(int currentStart, int prevEndExclusive)
    {
        return currentStart > prevEndExclusive;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SegmentsConnectOrOverlap8(int currentEndExclusive, int prevStartInclusive)
    {
        return currentEndExclusive >= prevStartInclusive;
    }
}
