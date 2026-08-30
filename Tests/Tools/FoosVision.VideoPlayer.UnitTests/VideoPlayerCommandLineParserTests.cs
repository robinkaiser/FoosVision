// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Windows.Decoding;
using FoosVision.VideoPlayer.Options;

namespace FoosVision.VideoPlayer.UnitTests;

public class VideoPlayerCommandLineParserTests
{
    private readonly VideoPlayerCommandLineParser _Parser = new();

    [Fact]
    public void Parse_accepts_required_arguments_and_sets_defaults()
    {
        VideoPlayerCommandLineParseResult result = _Parser.Parse(
        [
            "--file", "clip.mp4",
            "--codec", "h264",
            "--width", "1920",
            "--height", "1080",
        ]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Options);
        Assert.Equal("clip.mp4", result.Options.FilePath);
        Assert.Equal(CodecType.H264, result.Options.Codec);
        Assert.Equal(120, result.Options.EncodedFps);
        Assert.Equal(30, result.Options.DecodedFps);
        Assert.Equal(WindowsVideoDecoderHardwareMode.PreferHardware, result.Options.HardwareMode);
    }

    [Fact]
    public void Parse_accepts_optional_arguments()
    {
        VideoPlayerCommandLineParseResult result = _Parser.Parse(
        [
            "--file", "clip.mp4",
            "--codec", "h265",
            "--width", "1920",
            "--height", "1080",
            "--encoded-fps", "240",
            "--decoded-fps", "60",
            "--decode-mode", "software-only",
        ]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Options);
        Assert.Equal(CodecType.H265, result.Options.Codec);
        Assert.Equal(240, result.Options.EncodedFps);
        Assert.Equal(60, result.Options.DecodedFps);
        Assert.Equal(WindowsVideoDecoderHardwareMode.SoftwareOnly, result.Options.HardwareMode);
    }

    [Fact]
    public void Parse_returns_help_for_help_argument()
    {
        VideoPlayerCommandLineParseResult result = _Parser.Parse(["--help"]);

        Assert.True(result.IsSuccess);
        Assert.True(result.ShowHelp);
        Assert.Null(result.Options);
    }

    [Fact]
    public void Parse_rejects_missing_required_argument()
    {
        VideoPlayerCommandLineParseResult result = _Parser.Parse(
        [
            "--file", "clip.mp4",
            "--codec", "h264",
            "--width", "1920",
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing required argument '--height'.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_rejects_unknown_codec()
    {
        VideoPlayerCommandLineParseResult result = _Parser.Parse(
        [
            "--file", "clip.mp4",
            "--codec", "vp9",
            "--width", "1920",
            "--height", "1080",
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Argument '--codec' must be 'h264' or 'h265'.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_rejects_invalid_frame_rate_combination()
    {
        VideoPlayerCommandLineParseResult result = _Parser.Parse(
        [
            "--file", "clip.mp4",
            "--codec", "h264",
            "--width", "1920",
            "--height", "1080",
            "--encoded-fps", "120",
            "--decoded-fps", "50",
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal("DecodedFps must divide EncodedFps for deterministic playback sampling. (Parameter 'DecodedFps')", result.ErrorMessage);
    }
}
