// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings.Diagnostics;

public record DiagnosticsPaths(
    string Root,
    string Logs,
    string Videos);
