// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Core.EncodedVideo;

public record struct EncodedAccessUnit(
    long TimeNs,
    bool IsKeyFrame,
    bool ContainsAllRequiredParameterSets,
    int Offset,
    int Size);
