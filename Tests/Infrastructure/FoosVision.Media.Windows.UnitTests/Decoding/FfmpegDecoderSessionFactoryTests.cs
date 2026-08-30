// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;
using FFmpeg.AutoGen;
using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Windows.Decoding;
using FoosVision.Media.Windows.Decoding.Ffmpeg;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Windows.UnitTests.Decoding;

public class FfmpegDecoderSessionFactoryTests
{
    [Fact]
    public void Create_returns_software_session_when_hardware_is_preferred_but_not_supported()
    {
        FfmpegDecoderSessionFactory factory = new(
            new FakeHardwareConfigProvider([]),
            static hardwareConfig => new FakeSession(hardwareConfig));
        FfmpegDecoderOptions options = new(
            CodecType.H264,
            1920,
            1080,
            FrameByteFormat.RGBA8888,
            WindowsVideoDecoderHardwareMode.PreferHardware);

        using IFfmpegDecoderSession session = factory.Create(options);

        Assert.False(session.IsHardwareAccelerated);
    }

    [Fact]
    public void Create_returns_hardware_session_when_supported_config_exists()
    {
        FfmpegHardwareDecodeConfig hardwareConfig = new(AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA, AVPixelFormat.AV_PIX_FMT_D3D11);
        FfmpegDecoderSessionFactory factory = new(
            new FakeHardwareConfigProvider([hardwareConfig]),
            static config => new FakeSession(config));
        FfmpegDecoderOptions options = new(
            CodecType.H264,
            1920,
            1080,
            FrameByteFormat.RGBA8888,
            WindowsVideoDecoderHardwareMode.PreferHardware);

        using IFfmpegDecoderSession session = factory.Create(options);

        Assert.True(session.IsHardwareAccelerated);
    }

    [Fact]
    public void Create_throws_for_required_hardware_without_supported_config()
    {
        FfmpegDecoderSessionFactory factory = new(
            new FakeHardwareConfigProvider([]),
            static hardwareConfig => new FakeSession(hardwareConfig));
        FfmpegDecoderOptions options = new(
            CodecType.H264,
            1920,
            1080,
            FrameByteFormat.RGBA8888,
            WindowsVideoDecoderHardwareMode.RequireHardware);

        Assert.Throws<NotSupportedException>(() => factory.Create(options));
    }

    private class FakeHardwareConfigProvider(IReadOnlyList<FfmpegHardwareDecodeConfig> configs) : IFfmpegHardwareConfigProvider
    {
        public IReadOnlyList<FfmpegHardwareDecodeConfig> GetCompatibleHardwareConfigs(CodecType codec) => configs;
    }

    private class FakeSession(FfmpegHardwareDecodeConfig? hardwareConfig) : IFfmpegDecoderSession
    {
        public bool IsConfigured { get; private set; }

        public bool IsHardwareAccelerated => hardwareConfig.HasValue;

        public void Configure(FfmpegDecoderOptions options)
        {
            IsConfigured = true;
        }

        public void PushAccessUnit(ReadOnlySpan<byte> buffer, long timeNs, bool isKeyFrame, bool enqueueFrames = true)
        {
        }

        public bool TryDequeueFrame([NotNullWhen(true)] out FfmpegDecodedFrame? frame)
        {
            frame = null;
            return false;
        }

        public void Flush()
        {
        }

        public void Reset()
        {
        }

        public void Dispose()
        {
        }
    }
}
