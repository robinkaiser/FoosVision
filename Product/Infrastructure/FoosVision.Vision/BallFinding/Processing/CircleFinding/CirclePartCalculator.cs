// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.Common.Processing;

namespace FoosVision.Vision.BallFinding.Processing.CircleFinding;

public record class CirclePartCalculatorParameters
{
    // Expected circle radius
    public int ExpectedRadius { get; init; } = 20;

    // Minimum number of points a shape must consist of to be further processed
    public int MinimumShapePoints { get; init; } = 12;

    // Maximum number of points of a shape that will be used for processing (all other points will be discarded)
    public int MaximumShapePoints { get; init; } = 256;

    // Minimum number of points (Inliers) required to form a circle part
    public int MinimumCirclePartPoints { get; init; } = 6;

    // Random seed for random sample consensus
    public int RandomSeed { get; init; } = 42;

    public static readonly CirclePartCalculatorParameters Default = new();
}

public record struct SPoint
{
    public int X;
    public int Y;
}

public unsafe class CirclePartCalculator
{
    private struct Model
    {
        public double CenterX;
        public double CenterY;
        public double SquaredRadius;
        public double MeanInlierError;
        public int InlierCount;
    }

    // Threshold value to determine if the radius of a fitted circle is acceptable.
    private const double _MaxRadiusDeviationInPercent = 25;

    // Threshold to determine data points that are fit well by the model (inlier)
    private const int _MaxInlierCenterDistanceSquared = 3 * 3;

    // Maximum number of iterations
    private const double _MaxIterations = 128;

    private readonly CirclePartCalculatorParameters _Parameters;
    private readonly CircleModelSolver _Solver;
    private readonly Random _Random;
    private readonly int[] _MaybeInliers;
    private readonly SPoint[] _ConfirmedInliers;
    private readonly double _MinRadiusSquared;
    private readonly double _MaxRadiusSquared;

    private EdgePoint[] _Points = [];
    private int _PointCount;
    private Model _MaybeModel;
    private Model _BestFitModel;
    private int _ConfirmedInliersCount;

    public CirclePartCalculator(CirclePartCalculatorParameters parameters)
    {
        _Parameters = parameters;

        _Solver = new();
        _Random = new(parameters.RandomSeed);
        _MaybeInliers = new int[3];
        _ConfirmedInliers = new SPoint[parameters.MaximumShapePoints];

        double r = parameters.ExpectedRadius;
        double dr = r * _MaxRadiusDeviationInPercent / 100;
        _MinRadiusSquared = (r - dr) * (r - dr);
        _MaxRadiusSquared = (r + dr) * (r + dr);
    }

    public void ProcessEdges(EdgePoint[] points, int pointCount, SCircle[] outCircles, ref int outIndex)
    {
        _Points = points;
        _PointCount = Math.Min(pointCount, _Parameters.MaximumShapePoints);

        if (_PointCount < _Parameters.MinimumShapePoints)
        {   // Too few points, discard this shape
            return;
        }

        CalculateRandomSampleConsensus();

        if (_BestFitModel.InlierCount < 1)
        {
            return;
        }

        outCircles[outIndex].X = Convert.ToInt32(_BestFitModel.CenterX);
        outCircles[outIndex].Y = Convert.ToInt32(_BestFitModel.CenterY);
        outCircles[outIndex].Radius = Convert.ToInt32(Math.Sqrt(_BestFitModel.SquaredRadius));
        outCircles[outIndex].MeanPointError = _BestFitModel.MeanInlierError;
        outCircles[outIndex].PointCount = _BestFitModel.InlierCount;

        outIndex++;
    }

    private void CalculateRandomSampleConsensus()
    {   // https://en.wikipedia.org/wiki/Random_sample_consensus
        _BestFitModel.CenterX = 0;
        _BestFitModel.CenterY = 0;
        _BestFitModel.MeanInlierError = 0;
        _BestFitModel.InlierCount = 0;
        int iteration = 0;

        do
        {
            iteration++;

            // Select three random points
            RandomlySelectMaybeInliers();

            // Calculate maybe circle model based on the three randomly selected points
            bool success = CalculateMaybeModel();

            if (!success ||
                _MaybeModel.SquaredRadius < _MinRadiusSquared ||
                _MaybeModel.SquaredRadius > _MaxRadiusSquared)
            {
                continue;
            }

            // Select all inlier points that fit into the calculated maybe circle model
            GetConfirmedInliers();

            if (_ConfirmedInliersCount < _Parameters.MinimumCirclePartPoints) continue;

            // We may have found a good model.
            // Improve it by calculating a new model based on all inlier points (more than just three)
            var (x, y, rsquared) = _Solver.FitCircle(_ConfirmedInliersCount, _ConfirmedInliers);

            if (rsquared < _MinRadiusSquared ||
                rsquared > _MaxRadiusSquared)
            {   // "Improved" model does not fulfil radius constrain
                continue;
            }

            //// Calculate overall error for this new model
            //double thisError = 0.0;

            //for (int i = 0; i < _ConfirmedInliersCount; i++)
            //{
            //    double deltaX = _ConfirmedInliers[i].X - X;
            //    double deltaY = _ConfirmedInliers[i].Y - Y;
            //    double squaredDistanceFromCenter = deltaX * deltaX + deltaY * deltaY;
            //    thisError += Math.Abs(squaredDistanceFromCenter - Rsquared);
            //}

            //// Normalize error
            //thisError /= _ConfirmedInliersCount;

            // Reality shows: more points are better, the distance from centre is primarily just noise.
            // So simply use the number of inliers for now
            if (_ConfirmedInliersCount > _BestFitModel.InlierCount)
            {   // Better fit found!
                _BestFitModel.CenterX = x;
                _BestFitModel.CenterY = y;
                _BestFitModel.SquaredRadius = rsquared;
                _BestFitModel.MeanInlierError = 0.0; //  thisError;  => currently not used
                _BestFitModel.InlierCount = _ConfirmedInliersCount;
            }
        }
        while (iteration <= _MaxIterations);
    }

    private void RandomlySelectMaybeInliers()
    {
        _MaybeInliers[0] = -1;
        _MaybeInliers[1] = -1;
        _MaybeInliers[2] = -1;
        int count = 3;

        while (count > 0)
        {
            int next = _Random.Next(0, _PointCount);

            if (_MaybeInliers[0] == -1)
            {
                _MaybeInliers[0] = next;
                count--;
            }
            else if (_MaybeInliers[1] == -1)
            {
                if (_MaybeInliers[0] != next)
                {
                    _MaybeInliers[1] = next;
                    count--;
                }
            }
            else
            {
                if (_MaybeInliers[0] != next && _MaybeInliers[1] != next)
                {
                    _MaybeInliers[2] = next;
                    count--;
                }
            }
        }
    }

    private bool CalculateMaybeModel()
    {
        int c0 = _MaybeInliers[0];
        int c1 = _MaybeInliers[1];
        int c2 = _MaybeInliers[2];

        int c0Y = _Points[c0].Y;
        int c0X = _Points[c0].X;
        int c1Y = _Points[c1].Y;
        int c1X = _Points[c1].X;
        int c2Y = _Points[c2].Y;
        int c2X = _Points[c2].X;

        int nDeltaR10 = c1Y - c0Y;
        int nDeltaR21 = c2Y - c1Y;
        int nDeltaC01 = c0X - c1X;
        int nDeltaC12 = c1X - c2X;

        int nProductN10D21 = (nDeltaC01 * nDeltaR21) - (nDeltaR10 * nDeltaC12);

        if (nProductN10D21 == 0)
        {   // All points lie on one line or two points are identical. Circle fit is not possible
            return false;
        }

        // Solve equations for crossing point of normals
        int nDeltaR02 = c0Y - c2Y;
        int nDeltaC02 = c0X - c2X;
        int nSumR01 = c0Y + c1Y;
        int nSumC01 = c0X + c1X;

        double d2S = (double)((nDeltaR02 * nDeltaR21) - (nDeltaC02 * nDeltaC12)) / ((nDeltaR10 * nDeltaC12) - (nDeltaC01 * nDeltaR21));
        double dS = d2S / 2.0;

        // Calculate center point coordinates from line parameter
        double dDeltaCN0 = dS * nDeltaC01;
        double dCenterY = (nSumR01 / 2.0) + dDeltaCN0;
        double dDeltaRN0 = dS * nDeltaR10;
        double dCenterX = (nSumC01 / 2.0) + dDeltaRN0;

        double dDeltaY = c0Y - dCenterY;
        double dDeltaX = c0X - dCenterX;
        double dSquaredRadius = (dDeltaY * dDeltaY) + (dDeltaX * dDeltaX);

        _MaybeModel.CenterY = dCenterY;
        _MaybeModel.CenterX = dCenterX;
        _MaybeModel.SquaredRadius = dSquaredRadius;

        return true;
    }

    private void GetConfirmedInliers()
    {
        _ConfirmedInliersCount = 0;

        for (int i = 0; i < _PointCount; i++)
        {
            EdgePoint point = _Points[i];
            double deltaY = point.Y - _MaybeModel.CenterY;
            double deltaX = point.X - _MaybeModel.CenterX;
            double squaredDistanceFromCenter = (deltaY * deltaY) + (deltaX * deltaX);
            double error = Math.Abs(squaredDistanceFromCenter - _MaybeModel.SquaredRadius);

            if (error < _MaxInlierCenterDistanceSquared)
            {
                _ConfirmedInliers[_ConfirmedInliersCount].Y = point.Y;
                _ConfirmedInliers[_ConfirmedInliersCount].X = point.X;
                _ConfirmedInliersCount++;
            }
        }
    }
}
