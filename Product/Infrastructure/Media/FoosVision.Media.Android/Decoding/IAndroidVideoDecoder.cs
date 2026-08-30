// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;

namespace FoosVision.Media.Android.Decoding;

public interface IAndroidVideoDecoder : IDisposable
{
    bool IsConfigured { get; }

    AndroidVideoDecoderOptions? Options { get; }

    void Configure(AndroidVideoDecoderOptions options);

    void PushAccessUnit(ReadOnlySpan<byte> buffer, long timeNs, bool isKeyFrame, bool queueDecodedFrames = true);

    bool TryDequeueFrame([NotNullWhen(true)] out AndroidDecodedFrame? frame);

    void Flush();

    void Reset();
}
