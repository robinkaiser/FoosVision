// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Ports.Vision;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;

internal sealed class RecordingBallDetectionMaskDecoder : IEncodedBallDetectionMaskDecoder
{
    public List<EncodedBallDetectionMask> EncodedMasks { get; } = [];

    public void DecodeBallDetectionMask(EncodedBallDetectionMask mask, byte[] outputGray8)
    {
        EncodedMasks.Add(mask);
        Array.Copy(mask.Buffer, outputGray8, Math.Min(mask.Length, outputGray8.Length));
    }
}
