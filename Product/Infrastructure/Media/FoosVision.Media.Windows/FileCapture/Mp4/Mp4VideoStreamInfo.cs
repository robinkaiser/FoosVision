// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo;

namespace FoosVision.Media.Windows.FileCapture.Mp4;

internal record Mp4VideoStreamInfo(CodecType Codec, int Width, int Height);
