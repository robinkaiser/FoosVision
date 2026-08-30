// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Windows.Decoding.Ffmpeg;

internal class FfmpegDecoderSessionFactory : IFfmpegDecoderSessionFactory
{
    private readonly IFfmpegHardwareConfigProvider _HardwareConfigProvider;
    private readonly Func<FfmpegHardwareDecodeConfig?, IFfmpegDecoderSession> _SessionCreator;

    public FfmpegDecoderSessionFactory()
        : this(new FfmpegHardwareConfigProvider(), static hardwareConfig => new FfmpegDecoderSession(hardwareConfig))
    {
    }

    internal FfmpegDecoderSessionFactory(
        IFfmpegHardwareConfigProvider hardwareConfigProvider,
        Func<FfmpegHardwareDecodeConfig?, IFfmpegDecoderSession> sessionCreator)
    {
        _HardwareConfigProvider = hardwareConfigProvider;
        _SessionCreator = sessionCreator;
    }

    public IFfmpegDecoderSession Create(FfmpegDecoderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.HardwareMode == WindowsVideoDecoderHardwareMode.SoftwareOnly)
        {
            return _SessionCreator(null);
        }

        IReadOnlyList<FfmpegHardwareDecodeConfig> hardwareConfigs = _HardwareConfigProvider.GetCompatibleHardwareConfigs(options.Codec);
        if (hardwareConfigs.Count == 0)
        {
            if (options.HardwareMode == WindowsVideoDecoderHardwareMode.RequireHardware)
            {
                throw new NotSupportedException($"No compatible FFmpeg hardware decoder configuration found for codec '{options.Codec}'.");
            }

            return _SessionCreator(null);
        }

        return _SessionCreator(hardwareConfigs[0]);
    }
}
