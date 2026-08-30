// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.Possession;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.UnitTests.TrackingCore.Possession;

public class PossessionCalculatorTests
{
    private readonly PossessionCalculator _Testee;

    public PossessionCalculatorTests()
    {
        _Testee = new PossessionCalculator(TableConfig.Config);
    }

    [Fact]
    public void Fixture()
    {
    }

    [Fact]
    public void Ball_is_exactly_at_rods()
    {
        Assert.Equal(new(Team.A, PossessionArea.Defense), _Testee.Compute(new(100, 500)));
        Assert.Equal(new(Team.A, PossessionArea.Defense), _Testee.Compute(new(300, 500)));
        Assert.Equal(new(Team.B, PossessionArea.ThreeBar), _Testee.Compute(new(500, 500)));
        Assert.Equal(new(Team.A, PossessionArea.FiveBar), _Testee.Compute(new(700, 500)));
        Assert.Equal(new(Team.B, PossessionArea.FiveBar), _Testee.Compute(new(900, 500)));
        Assert.Equal(new(Team.A, PossessionArea.ThreeBar), _Testee.Compute(new(1100, 500)));
        Assert.Equal(new(Team.B, PossessionArea.Defense), _Testee.Compute(new(1300, 500)));
        Assert.Equal(new(Team.B, PossessionArea.Defense), _Testee.Compute(new(1500, 500)));
    }

    [Fact]
    public void Find_closest_bar_type_returns_real_rod()
    {
        Assert.True(_Testee.FindClosestBarType(new(100, 500)).TryGetValue(out BarType barType));
        Assert.Equal(BarType.A1, barType);

        Assert.True(_Testee.FindClosestBarType(new(1500, 500)).TryGetValue(out barType));
        Assert.Equal(BarType.B1, barType);
    }

    [Fact]
    public void Ball_is_between_the_rods()
    {
        Assert.Equal(new(Team.A, PossessionArea.Defense), _Testee.Compute(new(0, 200)));
        Assert.Equal(new(Team.A, PossessionArea.Defense), _Testee.Compute(new(399, 200)));

        Assert.Equal(new(Team.B, PossessionArea.ThreeBar), _Testee.Compute(new(401, 300)));
        Assert.Equal(new(Team.B, PossessionArea.ThreeBar), _Testee.Compute(new(599, 300)));

        Assert.Equal(new(Team.A, PossessionArea.FiveBar), _Testee.Compute(new(601, 400)));
        Assert.Equal(new(Team.A, PossessionArea.FiveBar), _Testee.Compute(new(799, 400)));

        Assert.Equal(new(Team.B, PossessionArea.FiveBar), _Testee.Compute(new(801, 500)));
        Assert.Equal(new(Team.B, PossessionArea.FiveBar), _Testee.Compute(new(999, 500)));

        Assert.Equal(new(Team.A, PossessionArea.ThreeBar), _Testee.Compute(new(1001, 600)));
        Assert.Equal(new(Team.A, PossessionArea.ThreeBar), _Testee.Compute(new(1199, 600)));

        Assert.Equal(new(Team.B, PossessionArea.Defense), _Testee.Compute(new(1201, 700)));
        Assert.Equal(new(Team.B, PossessionArea.Defense), _Testee.Compute(new(2000, 700)));
    }
}
