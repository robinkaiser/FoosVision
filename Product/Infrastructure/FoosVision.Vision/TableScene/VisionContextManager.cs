// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Ports.Vision;
using FoosVision.Vision.Common;
using FoosVision.Vision.TableScene.Processing;

namespace FoosVision.Vision.TableScene;

public class VisionContextManager
{
    private const int _MaxEncodedContextLength = 8 * 1024 * 1024;

    private readonly int _Width;
    private readonly int _Height;
    private readonly byte[] _ColorResponseImage;
    private readonly byte[] _EncodedVisionContextBuffer;
    private readonly int[] _VisionContextValueCounts;
    private readonly int[] _VisionContextPaletteValues;
    private readonly ushort[] _VisionContextValueIndices;

    private PlayerColorExclusionContext _PlayerColorExclusion;

    public VisionContextManager(int width, int height, byte[] colorResponseImage)
    {
        _Width = width;
        _Height = height;
        _ColorResponseImage = colorResponseImage;
        _EncodedVisionContextBuffer = new byte[_MaxEncodedContextLength];
        _VisionContextValueCounts = new int[VisionContextCodec.QuantizedColorCount];
        _VisionContextPaletteValues = new int[VisionContextCodec.QuantizedColorCount];
        _VisionContextValueIndices = new ushort[VisionContextCodec.QuantizedColorCount];
    }

    public PlayerColorExclusionContext PlayerColorExclusion
    {
        get => _PlayerColorExclusion;
        set => _PlayerColorExclusion = value;
    }

    public bool TryGetEncodedVisionContext(out EncodedVisionContext context)
    {
        if (!VisionContextCodec.TryEncode(_Width, _Height, _ColorResponseImage, _PlayerColorExclusion,
            _EncodedVisionContextBuffer, _VisionContextValueCounts, _VisionContextPaletteValues,
            _VisionContextValueIndices, out int encodedLength))
        {
            context = default;
            return false;
        }

        context = new EncodedVisionContext(_EncodedVisionContextBuffer, encodedLength);
        return true;
    }

    public bool TryApplyEncodedVisionContext(EncodedVisionContext context)
    {
        if (!VisionContextCodec.TryDecode(context.Buffer, context.Length, _ColorResponseImage,
            _VisionContextPaletteValues, out PlayerColorExclusionContext playerColorExclusion))
        {
            return false;
        }

        _PlayerColorExclusion = playerColorExclusion;
        return true;
    }
}
