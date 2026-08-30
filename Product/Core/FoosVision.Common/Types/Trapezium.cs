// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Common.Types;

public readonly record struct Trapezium(
    Point UpperLeft,
    Point UpperRight,
    Point LowerLeft,
    Point LowerRight);
