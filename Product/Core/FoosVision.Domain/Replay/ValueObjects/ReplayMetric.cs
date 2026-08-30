// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Domain.Replay.ValueObjects;

public record ReplayMetric(string Name, double Value, string Unit);
