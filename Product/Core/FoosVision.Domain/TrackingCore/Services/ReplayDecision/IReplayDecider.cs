// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.ReplayDecision;

public interface IReplayDecider
{
    Option<ReplayAnchor> Decide(Frame frame, bool isBallObserved, ReplayCandidate? candidate);

    void UpdateTableConfig(TableConfiguration tableConfig);
}
