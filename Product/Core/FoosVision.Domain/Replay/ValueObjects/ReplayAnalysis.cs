// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Domain.Replay.ValueObjects;

public record ReplayAnalysis(IReadOnlyList<ReplayAnalysisFrame> Frames);
