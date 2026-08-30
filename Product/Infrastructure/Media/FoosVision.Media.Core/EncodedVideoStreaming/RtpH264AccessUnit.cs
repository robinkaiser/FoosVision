// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Core.EncodedVideoStreaming;

public readonly record struct RtpH264AccessUnit(byte[] Buffer, long TimeNs, bool IsKeyFrame);
