// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Core.Speech;

/// <summary>Maps complete, fixed announcement texts to bundled recording names.</summary>
public static class SpeechRecordingCatalog
{
    public static string? Find(string text) => text switch
    {
        "Left" => "speech_left",
        "Right" => "speech_right",
        "Middle" => "speech_middle",
        "Short" => "speech_short",
        "Long" => "speech_long",
        "Wall" => "speech_wall",
        "Lane" => "speech_lane",
        "Split" => "speech_split",
        "Left ready" => "speech_left_ready",
        "Right ready" => "speech_right_ready",
        "Middle ready" => "speech_middle_ready",
        "Short ready" => "speech_short_ready",
        "Long ready" => "speech_long_ready",
        "Wall ready" => "speech_wall_ready",
        "Lane ready" => "speech_lane_ready",
        "Split ready" => "speech_split_ready",
        "Snake ready" => "speech_snake_ready",
        "Pull ready" => "speech_pull_ready",
        "Pass ready" => "speech_pass_ready",
        "Shoot!" => "speech_shoot",
        "Aborted" => "speech_aborted",
        _ => null,
    };
}
