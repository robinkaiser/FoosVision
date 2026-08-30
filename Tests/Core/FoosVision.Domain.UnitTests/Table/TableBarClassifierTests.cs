// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.Services;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.UnitTests.Table;

public class TableBarClassifierTests
{
    [Theory]
    [InlineData(BarType.A1)]
    [InlineData(BarType.A2)]
    [InlineData(BarType.A5)]
    [InlineData(BarType.A3)]
    public void GetTeam_Returns_Team_A_For_A_Bars(BarType barType)
    {
        Assert.Equal(Team.A, TableBarClassifier.GetTeam(barType));
    }

    [Theory]
    [InlineData(BarType.B3)]
    [InlineData(BarType.B5)]
    [InlineData(BarType.B2)]
    [InlineData(BarType.B1)]
    public void GetTeam_Returns_Team_B_For_B_Bars(BarType barType)
    {
        Assert.Equal(Team.B, TableBarClassifier.GetTeam(barType));
    }

    [Theory]
    [InlineData(BarType.A1)]
    [InlineData(BarType.A2)]
    [InlineData(BarType.B1)]
    [InlineData(BarType.B2)]
    public void GetPossessionArea_Returns_Defense_For_Defense_Bars(BarType barType)
    {
        Assert.Equal(PossessionArea.Defense, TableBarClassifier.GetPossessionArea(barType));
        Assert.True(TableBarClassifier.IsDefenseBar(barType));
        Assert.False(TableBarClassifier.IsThreeBar(barType));
    }

    [Theory]
    [InlineData(BarType.A5)]
    [InlineData(BarType.B5)]
    public void GetPossessionArea_Returns_FiveBar_For_Five_Bars(BarType barType)
    {
        Assert.Equal(PossessionArea.FiveBar, TableBarClassifier.GetPossessionArea(barType));
        Assert.False(TableBarClassifier.IsDefenseBar(barType));
        Assert.False(TableBarClassifier.IsThreeBar(barType));
    }

    [Theory]
    [InlineData(BarType.A3)]
    [InlineData(BarType.B3)]
    public void GetPossessionArea_Returns_ThreeBar_For_Three_Bars(BarType barType)
    {
        Assert.Equal(PossessionArea.ThreeBar, TableBarClassifier.GetPossessionArea(barType));
        Assert.False(TableBarClassifier.IsDefenseBar(barType));
        Assert.True(TableBarClassifier.IsThreeBar(barType));
    }

    [Fact]
    public void Invalid_BarType_Maps_To_None()
    {
        BarType invalid = (BarType)999;

        Assert.Equal(Team.None, TableBarClassifier.GetTeam(invalid));
        Assert.Equal(PossessionArea.None, TableBarClassifier.GetPossessionArea(invalid));
        Assert.False(TableBarClassifier.IsDefenseBar(invalid));
        Assert.False(TableBarClassifier.IsThreeBar(invalid));
    }
}
