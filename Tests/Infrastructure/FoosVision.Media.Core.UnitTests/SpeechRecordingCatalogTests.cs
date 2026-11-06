// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.Speech;

namespace FoosVision.Media.Core.UnitTests;

public class SpeechRecordingCatalogTests
{
    [Theory]
    [InlineData("Left", "speech_left")]
    [InlineData("Right", "speech_right")]
    [InlineData("Middle", "speech_middle")]
    [InlineData("Short", "speech_short")]
    [InlineData("Long", "speech_long")]
    [InlineData("Wall", "speech_wall")]
    [InlineData("Lane", "speech_lane")]
    [InlineData("Split", "speech_split")]
    [InlineData("Left ready", "speech_left_ready")]
    [InlineData("Right ready", "speech_right_ready")]
    [InlineData("Middle ready", "speech_middle_ready")]
    [InlineData("Short ready", "speech_short_ready")]
    [InlineData("Long ready", "speech_long_ready")]
    [InlineData("Wall ready", "speech_wall_ready")]
    [InlineData("Lane ready", "speech_lane_ready")]
    [InlineData("Split ready", "speech_split_ready")]
    [InlineData("Snake ready", "speech_snake_ready")]
    [InlineData("Pull ready", "speech_pull_ready")]
    [InlineData("Pass ready", "speech_pass_ready")]
    [InlineData("Shoot!", "speech_shoot")]
    [InlineData("Aborted", "speech_aborted")]
    public void Find_selects_recordings_for_complete_fixed_announcements(string text, string recording)
    {
        Assert.Equal(recording, SpeechRecordingCatalog.Find(text));
    }

    [Theory]
    [InlineData("Links")]
    [InlineData("Links ready")]
    [InlineData("Left or right")]
    [InlineData("My custom variant")]
    [InlineData("../left")]
    [InlineData("Tick tack ready")]
    public void Find_leaves_unknown_texts_for_default_text_to_speech(string text)
    {
        Assert.Null(SpeechRecordingCatalog.Find(text));
    }
}
