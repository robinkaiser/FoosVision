// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Core.EncodedVideo;

public readonly record struct AccessUnitDispatch(
    ReadOnlyMemory<byte> Buffer,
    long TimeNs,
    bool IsKeyFrame,
    bool QueueDecodedFrames);
