// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Buffers;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Android.Decoding;

public class AndroidDecodedFrame : IDisposable
{
    private readonly bool _ReturnBufferToPool;
    private byte[]? _Buffer;

    public AndroidDecodedFrame(
        long timeNs,
        int width,
        int height,
        int stride,
        FrameByteFormat format,
        byte[] buffer,
        int bufferLength,
        bool returnBufferToPool = false)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bufferLength, buffer.Length);

        TimeNs = timeNs;
        Width = width;
        Height = height;
        Stride = stride;
        Format = format;
        BufferLength = bufferLength;
        _Buffer = buffer;
        _ReturnBufferToPool = returnBufferToPool;
    }

    public long TimeNs { get; }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public FrameByteFormat Format { get; }

    public int BufferLength { get; }

    public FoosVision.Ports.Media.FrameLayout Layout => new(Format, Width, Height, Stride);

    public byte[] Buffer => _Buffer ?? throw new ObjectDisposedException(nameof(AndroidDecodedFrame));

    public ReadOnlySpan<byte> AsSpan() => Buffer.AsSpan(0, BufferLength);

    public void Dispose()
    {
        byte[]? buffer = _Buffer;
        _Buffer = null;

        if (_ReturnBufferToPool && buffer != null)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        GC.SuppressFinalize(this);
    }
}
