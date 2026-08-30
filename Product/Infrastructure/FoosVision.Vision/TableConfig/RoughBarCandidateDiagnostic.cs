// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.TableConfig.Processing.HoughLines;

namespace FoosVision.Vision.TableConfig;

public readonly record struct RoughBarCandidateDiagnostic(
    int Index,
    HoughLine Line,
    LineCoverageScore CoverageScore,
    bool Selected);
