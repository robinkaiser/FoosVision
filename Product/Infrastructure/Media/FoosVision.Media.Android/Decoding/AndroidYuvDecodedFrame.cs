// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Android.Decoding;

public class AndroidYuvDecodedFrame : IYuvFrameHandle
{
    private readonly IYuvFrameHandle _Handle;

    public AndroidYuvDecodedFrame(IYuvFrameHandle handle)
    {
        _Handle = handle;
    }

    public Frame Meta => _Handle.Meta;

    public long TimeNs => Meta.TimestampNs;

    public int Width => Layout.Width;

    public int Height => Layout.Height;

    public YuvFrameLayout Layout => _Handle.Layout;

    public byte[] BufferY => _Handle.BufferY;

    public byte[] BufferU => _Handle.BufferU;

    public byte[] BufferV => _Handle.BufferV;

    public void Release()
    {
        _Handle.Release();
    }
}
