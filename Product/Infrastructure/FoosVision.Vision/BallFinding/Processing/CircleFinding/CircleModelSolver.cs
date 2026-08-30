// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Vision.BallFinding.Processing.CircleFinding;

public class CircleModelSolver
{
    private readonly double[] _AtA;
    private readonly double[] _Atb;

    public CircleModelSolver()
    {
        _AtA = new double[9];
        _Atb = new double[3];
    }

    public (double X, double Y, double Rsquared) FitCircle(int pointCount, SPoint[] pointData)
    {
        // Solving AtAx = Atb
        // https://matrixcalc.org/

        _AtA[0] = 0;
        _AtA[1] = 0;
        _AtA[2] = 0;
        _AtA[4] = 0;
        _AtA[5] = 0;

        _Atb[0] = 0;
        _Atb[1] = 0;
        _Atb[2] = 0;

        for (int i = 0; i < pointCount; i++)
        {
            double x = pointData[i].X;
            double y = pointData[i].Y;

            double xx = x * x;
            double yy = y * y;
            double xy = x * y;

            _AtA[0] += xx;
            _AtA[1] += xy;
            _AtA[2] += x;
            _AtA[4] += yy;
            _AtA[5] += y;

            double xx_plus_yy = xx + yy;

            _Atb[0] += x * xx_plus_yy;
            _Atb[1] += y * xx_plus_yy;
            _Atb[2] += xx_plus_yy;
        }

        _AtA[0] *= 4;
        _AtA[1] *= 4;
        _AtA[2] *= 2;
        _AtA[3] = _AtA[1];
        _AtA[4] *= 4;
        _AtA[5] *= 2;
        _AtA[6] = _AtA[2];
        _AtA[7] = _AtA[5];
        _AtA[8] = pointCount;

        _Atb[0] *= 2;
        _Atb[1] *= 2;

        // Gaussian elimination of augmented matrix
        // ( _AtA[0] _AtA[1] _AtA[2] | _AtB[0] )
        // ( _AtA[3] _AtA[4] _AtA[5] | _AtB[1] )
        // ( _AtA[6] _AtA[7] _AtA[8] | _AtB[2] )

        // _AtA[1,0] => 0
        double d = _AtA[3] / _AtA[0];
        _AtA[3] = 0;
        _AtA[4] -= _AtA[1] * d;
        _AtA[5] -= _AtA[2] * d;
        _Atb[1] -= _Atb[0] * d;

        // _AtA[2,0] => 0
        d = _AtA[6] / _AtA[0];
        _AtA[6] = 0;
        _AtA[7] -= _AtA[1] * d;
        _AtA[8] -= _AtA[2] * d;
        _Atb[2] -= _Atb[0] * d;

        // _AtA[2,1] => 0
        d = _AtA[7] / _AtA[4];
        _AtA[7] = 0;
        _AtA[8] -= _AtA[5] * d;
        _Atb[2] -= _Atb[1] * d;

        double p3 = _Atb[2] / _AtA[8];
        double p2 = (_Atb[1] - (_AtA[5] * p3)) / _AtA[4];
        double p1 = (_Atb[0] - (_AtA[2] * p3) - (_AtA[1] * p2)) / _AtA[0];

        double rSquared = (p1 * p1) + (p2 * p2) + p3;

        return (p1, p2, rSquared);
    }
}
