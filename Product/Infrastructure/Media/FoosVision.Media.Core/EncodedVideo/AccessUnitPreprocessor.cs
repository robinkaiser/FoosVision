// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo.AnnexB;

namespace FoosVision.Media.Core.EncodedVideo;

public class AccessUnitPreprocessor
{
    private static readonly ParameterSetType[] _H264ParameterSetOrder = [ParameterSetType.SPS, ParameterSetType.PPS];
    private static readonly ParameterSetType[] _H265ParameterSetOrder = [ParameterSetType.VPS, ParameterSetType.SPS, ParameterSetType.PPS];

    private readonly Dictionary<ParameterSetType, byte[]> _ParameterSets;
    private readonly AnnexBNalRange[] _ParsedNalRanges;

    public AccessUnitPreprocessor()
    {
        _ParameterSets = [];
        _ParsedNalRanges = new AnnexBNalRange[32];
    }

    public bool TryPrepare(
        ReadOnlySpan<byte> buffer,
        CodecType codec,
        long timeNs,
        bool isKeyFrame,
        bool queueDecodedFrames,
        IList<AccessUnitDispatch> dispatches)
    {
        ArgumentNullException.ThrowIfNull(dispatches);

        if (buffer.IsEmpty)
        {
            throw new ArgumentException("Access unit must not be empty.", nameof(buffer));
        }

        byte[] accessUnit = buffer.ToArray();
        ParsedAccessUnit parsedAccessUnit = ParseAccessUnit(accessUnit, codec);

        if (!parsedAccessUnit.IsValid || !parsedAccessUnit.HasVclNal || !HasRequiredParameterSets(codec))
        {
            return false;
        }

        if (isKeyFrame && !parsedAccessUnit.ContainsAllRequiredParameterSets)
        {
            AddParameterSetDispatches(codec, timeNs, dispatches);
        }

        dispatches.Add(new AccessUnitDispatch(accessUnit, timeNs, isKeyFrame, queueDecodedFrames));
        return true;
    }

    public void Reset()
    {
        _ParameterSets.Clear();
    }

    private ParsedAccessUnit ParseAccessUnit(byte[] accessUnit, CodecType codec)
    {
        int count = AnnexBParser.FindNals(accessUnit, 0, accessUnit.Length, _ParsedNalRanges, _ParsedNalRanges.Length);
        if (count <= 0)
        {
            return ParsedAccessUnit.Invalid;
        }

        bool hasVclNal = false;
        bool hasVps = false;
        bool hasSps = false;
        bool hasPps = false;

        for (int i = 0; i < count; i++)
        {
            AnnexBNalRange range = _ParsedNalRanges[i];
            byte headerByte = accessUnit[range.HeaderOffset];
            int nalUnitType = AnnexBNalClassifier.GetNalUnitType(codec, headerByte);
            ParameterSetType parameterSetType = AnnexBNalClassifier.GetParameterSetType(codec, nalUnitType);

            if (parameterSetType != ParameterSetType.Invalid)
            {
                _ParameterSets[parameterSetType] = accessUnit[range.StartOffset..range.EndOffsetExclusive];

                switch (parameterSetType)
                {
                    case ParameterSetType.VPS:
                        hasVps = true;
                        break;
                    case ParameterSetType.SPS:
                        hasSps = true;
                        break;
                    case ParameterSetType.PPS:
                        hasPps = true;
                        break;
                }
            }

            if (AnnexBNalClassifier.IsVclNalUnit(codec, nalUnitType))
            {
                hasVclNal = true;
            }
        }

        bool containsAllRequiredParameterSets = codec switch
        {
            CodecType.H264 => hasSps && hasPps,
            CodecType.H265 => hasVps && hasSps && hasPps,
            _ => false,
        };

        return new ParsedAccessUnit(hasVclNal, containsAllRequiredParameterSets, IsValid: true);
    }

    private bool HasRequiredParameterSets(CodecType codec)
    {
        ParameterSetType[] requiredSets = GetRequiredParameterSetOrder(codec);

        foreach (ParameterSetType parameterSetType in requiredSets)
        {
            if (!_ParameterSets.ContainsKey(parameterSetType))
            {
                return false;
            }
        }

        return requiredSets.Length != 0;
    }

    private void AddParameterSetDispatches(CodecType codec, long timeNs, IList<AccessUnitDispatch> dispatches)
    {
        foreach (ParameterSetType parameterSetType in GetRequiredParameterSetOrder(codec))
        {
            if (_ParameterSets.TryGetValue(parameterSetType, out byte[]? accessUnit))
            {
                dispatches.Add(new AccessUnitDispatch(accessUnit, timeNs, IsKeyFrame: false, QueueDecodedFrames: false));
            }
        }
    }

    private static ParameterSetType[] GetRequiredParameterSetOrder(CodecType codec)
        => codec switch
        {
            CodecType.H264 => _H264ParameterSetOrder,
            CodecType.H265 => _H265ParameterSetOrder,
            _ => []
        };

    private readonly record struct ParsedAccessUnit(bool HasVclNal, bool ContainsAllRequiredParameterSets, bool IsValid)
    {
        public static ParsedAccessUnit Invalid => new(false, false, false);
    }
}
