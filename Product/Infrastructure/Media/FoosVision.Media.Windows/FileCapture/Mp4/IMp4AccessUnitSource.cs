// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;

namespace FoosVision.Media.Windows.FileCapture.Mp4;

internal interface IMp4AccessUnitSource : IDisposable
{
    Mp4VideoStreamInfo StreamInfo { get; }

    void Configure(string filePath);

    void Reset();

    bool TryReadNextAccessUnit([NotNullWhen(true)] out Mp4AccessUnit? accessUnit);
}
