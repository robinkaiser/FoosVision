// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Settings.Diagnostics;

namespace FoosVision.Settings;

public record SettingsPaths(
    string Root,
    string Config,
    string ExampleConfig,
    DiagnosticsPaths Diagnostics);
