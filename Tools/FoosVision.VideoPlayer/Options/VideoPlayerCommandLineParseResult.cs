// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.VideoPlayer.Options;

public record VideoPlayerCommandLineParseResult(
    bool IsSuccess,
    bool ShowHelp,
    VideoPlayerOptions? Options,
    string? ErrorMessage);
