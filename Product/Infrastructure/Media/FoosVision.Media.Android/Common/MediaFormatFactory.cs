// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Media;

namespace FoosVision.Media.Android.Common;

internal static class MediaFormatFactory
{
    public const string MimeTypeAvc = MediaFormat.MimetypeVideoAvc;
    public const string MimeTypeHevc = MediaFormat.MimetypeVideoHevc;

    private const float _KeyFrameIntervalSeconds = 0.25f;
    private const int _BitRate = 8_000_000;

    public static MediaFormat CreateAvc(int width, int height, int fps)
        => CreateFormat(MimeTypeAvc, width, height, fps);

    public static MediaFormat CreateHevc(int width, int height, int fps)
        => CreateFormat(MimeTypeHevc, width, height, fps);

    public static MediaFormat CreateFormat(string mime, int width, int height, int fps)
    {
        var format = MediaFormat.CreateVideoFormat(mime, width, height);

        format.SetInteger(MediaFormat.KeyBitRate, _BitRate);
        format.SetInteger(MediaFormat.KeyColorFormat, (int)MediaCodecCapabilities.Formatsurface);
        format.SetInteger(MediaFormat.KeyFrameRate, fps);
        format.SetFloat(MediaFormat.KeyIFrameInterval, _KeyFrameIntervalSeconds);

        // TODO?
        // Some decoders/streaming setups benefit from SPS/PPS on each IDR.
        // Not universally supported; safe to ignore if the platform does not use it.
        // If this is not supported, Codec.Configure() may throw! In this case Configure
        // must be repeated with a mediaformat without this flag set
        //format.SetInteger(MediaFormat.KeyPrependHeaderToSyncFrames, 1);

        return format;
    }
}
