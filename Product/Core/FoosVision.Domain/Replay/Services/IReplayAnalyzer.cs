// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.ValueObjects;

namespace FoosVision.Domain.Replay.Services;

public interface IReplayAnalyzer
{
    ReplayAnalysis Analyze(IEnumerable<ReplayTrackedFrame> trackedFrames);
}
