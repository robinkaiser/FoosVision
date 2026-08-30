// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;

namespace FoosVision.Media.Windows.Decoding;

public interface IWindowsVideoDecoder : IDisposable
{
    bool IsConfigured { get; }

    bool IsHardwareAccelerated { get; }

    WindowsVideoDecoderOptions? Options { get; }

    void Configure(WindowsVideoDecoderOptions options);

    void PushAccessUnit(ReadOnlySpan<byte> buffer, long timeNs, bool isKeyFrame, bool queueDecodedFrames = true);

    bool TryDequeueFrame([NotNullWhen(true)] out WindowsDecodedFrame? frame);

    void Flush();

    void Reset();
}
