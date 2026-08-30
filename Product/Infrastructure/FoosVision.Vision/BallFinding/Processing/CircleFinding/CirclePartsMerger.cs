// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Runtime.CompilerServices;

namespace FoosVision.Vision.BallFinding.Processing.CircleFinding;

public struct SCircle
{
    public int X;
    public int Y;
    public int Radius;
    public double MeanPointError;
    public int PointCount;
}

public unsafe class CirclePartsMerger
{
    private const int _EliminatedCirclePointCountMarker = -1;

    private readonly int _MaximumSquaredCirclePartsDistance; // Maximum distance between two circle parts that form one circle
    private readonly int _MinimumSquaredFullCircleDistance; // Minimum distance between two circles

    private readonly SCircle[] _Circles;
    private int _Count;

    public CirclePartsMerger(int expectedR, int maxCircleParts)
    {
        _MaximumSquaredCirclePartsDistance = expectedR * expectedR / 16;
        _MinimumSquaredFullCircleDistance = expectedR * expectedR * 4; // d * d

        _Circles = new SCircle[maxCircleParts];
    }

    public SCircle[] CirclePartBuffer => _Circles;

    public IEnumerable<SCircle> MergeCircles(int count)
    {
        _Count = count;

        SortCircles();
        MergeCircleParts();

        SortCircles();
        DiscardOverlappingCircles();

        foreach (var circle in _Circles.Take(_Count))
        {
            if (circle.PointCount != _EliminatedCirclePointCountMarker) yield return circle;
        }
    }

    private void MergeCircleParts()
    {
        for (int i = 0; i < _Count - 1; i++)
        {
            if (_Circles[i].PointCount == _EliminatedCirclePointCountMarker) continue;

            for (int n = i + 1; n < _Count; n++)
            {
                if (_Circles[n].PointCount == _EliminatedCirclePointCountMarker) continue;

                int dSquared = SquaredDistance(in _Circles[i], in _Circles[n]);

                if (dSquared <= _MaximumSquaredCirclePartsDistance)
                {   // Merge into circle i
                    _Circles[i].X = (int)((_Circles[i].X + _Circles[n].X) / 2.0);
                    _Circles[i].Y = (int)((_Circles[i].Y + _Circles[n].Y) / 2.0);
                    _Circles[i].Radius = (int)((_Circles[i].Radius + _Circles[n].Radius) / 2.0);
                    _Circles[i].MeanPointError = (_Circles[i].MeanPointError + _Circles[n].MeanPointError) / 2.0;
                    _Circles[i].PointCount += _Circles[n].PointCount;

                    // Eliminate circle n
                    _Circles[n].PointCount = _EliminatedCirclePointCountMarker;
                }
            }
        }
    }

    private void DiscardOverlappingCircles()
    {
        for (int i = 0; i < _Count - 1; i++)
        {
            if (_Circles[i].PointCount == _EliminatedCirclePointCountMarker) continue;

            for (int n = i + 1; n < _Count; n++)
            {
                if (_Circles[n].PointCount == _EliminatedCirclePointCountMarker) continue;

                int dSquared = SquaredDistance(in _Circles[i], in _Circles[n]);

                if (dSquared < _MinimumSquaredFullCircleDistance)
                {   // Eliminate circle n
                    _Circles[n].PointCount = _EliminatedCirclePointCountMarker;
                }
            }
        }
    }

    private void SortCircles()
    {
        Array.Sort(_Circles, 0, _Count, Comparer<SCircle>.Create((a, b) => b.PointCount.CompareTo(a.PointCount)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SquaredDistance(in SCircle a, in SCircle b)
    {
        int dX = a.X - b.X;
        int dY = a.Y - b.Y;

        return (dY * dY) + (dX * dX);
    }
}
