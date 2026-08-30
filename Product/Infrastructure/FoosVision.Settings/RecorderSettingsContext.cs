// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings;

public record RecorderSettingsContext(
    SettingsPaths Paths,
    RecorderSettings Settings,
    SettingsConfigSource ConfigSource,
    string? ConfigError);
