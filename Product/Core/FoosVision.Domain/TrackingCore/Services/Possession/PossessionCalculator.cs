// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.Services;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.Possession;

public class PossessionCalculator : IPossessionCalculator
{
    private TableConfiguration _TableConfig;

    public PossessionCalculator(TableConfiguration tableConfig)
    {
        _TableConfig = tableConfig;
    }

    public void UpdateTableConfig(TableConfiguration tableConfig)
    {
        _TableConfig = tableConfig;
    }

    public BallPossession Compute(Point ballPosition)
    {
        if (!FindClosestBarType(ballPosition).TryGetValue(out BarType type))
        {
            return BallPossession.None;
        }

        Team team = TableBarClassifier.GetTeam(type);
        PossessionArea area = TableBarClassifier.GetPossessionArea(type);

        return new BallPossession(team, area);
    }

    public Option<BarType> FindClosestBarType(Point ballPosition)
    {
        var bars = _TableConfig.Field.Bars.All;
        var closestBar = bars.MinBy(b => GetPointDistanceToLineSegment(ballPosition, b.Center.P0, b.Center.P1));

        return closestBar == null
            ? Option<BarType>.None()
            : Option<BarType>.Some(closestBar.Type);
    }

    private static double GetPointDistanceToLineSegment(Point p, Point a, Point b)
    {
        Vector2 a2b = new(b.X - a.X, b.Y - a.Y);
        Vector2 a2p = new(p.X - a.X, p.Y - a.Y);

        // Project vector ap onto vector ab to find the closest point
        double ab2 = (a2b.X * a2b.X) + (a2b.Y * a2b.Y);

        if (ab2 == 0.0)
        {   // a == b
            return Math.Sqrt(((p.X - a.X) * (p.X - a.X)) + ((p.Y - a.Y) * (p.Y - a.Y)));
        }

        double ap_ab = (a2p.X * a2p.X) + (a2p.Y * a2p.Y);
        double t = ap_ab / ab2;

        if (t < 0.0)
        {   // Closest to a
            return Math.Sqrt(((p.X - a.X) * (p.X - a.X)) + ((p.Y - a.Y) * (p.Y - a.Y)));
        }
        else if (t > 1.0)
        {   // Closest to b
            return Math.Sqrt(((p.X - b.X) * (p.X - b.X)) + ((p.Y - b.Y) * (p.Y - b.Y)));
        }

        // Projection point is on the segment
        Point prj = new(a.X + (t * a2b.X), a.Y + (t * a2b.Y));
        return Math.Sqrt(((p.X - prj.X) * (p.X - prj.X)) + ((p.Y - prj.Y) * (p.Y - prj.Y)));
    }
}
