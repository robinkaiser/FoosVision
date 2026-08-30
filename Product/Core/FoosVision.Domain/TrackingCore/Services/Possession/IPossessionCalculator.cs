// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.Possession;

public interface IPossessionCalculator
{
    void UpdateTableConfig(TableConfiguration tableConfig);

    BallPossession Compute(Point ballPosition);

    Option<BarType> FindClosestBarType(Point ballPosition);
}
