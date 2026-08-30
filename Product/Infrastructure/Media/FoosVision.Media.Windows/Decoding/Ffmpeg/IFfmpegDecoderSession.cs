// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;

namespace FoosVision.Media.Windows.Decoding.Ffmpeg;

internal interface IFfmpegDecoderSession : IDisposable
{
    bool IsConfigured { get; }

    bool IsHardwareAccelerated { get; }

    void Configure(FfmpegDecoderOptions options);

    void PushAccessUnit(ReadOnlySpan<byte> buffer, long timeNs, bool isKeyFrame, bool enqueueFrames = true);

    bool TryDequeueFrame([NotNullWhen(true)] out FfmpegDecodedFrame? frame);

    void Flush();

    void Reset();
}
