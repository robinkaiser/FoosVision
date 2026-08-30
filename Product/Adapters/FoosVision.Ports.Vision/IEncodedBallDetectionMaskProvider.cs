// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Ports.Vision;

public readonly record struct EncodedBallDetectionMask(byte[] Buffer, int Length, int Width, int Height);

public interface IEncodedBallDetectionMaskProvider
{
    void GetEncodedBallDetectionMask(out EncodedBallDetectionMask mask);
}

public interface IEncodedBallDetectionMaskDecoder
{
    void DecodeBallDetectionMask(EncodedBallDetectionMask mask, byte[] outputGray8);
}
