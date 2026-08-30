// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Adapters.Viewer.Connectivity;

public interface IRecorderConnectionService
{
    Task<RecorderConnectionResult> ConnectAsync(CancellationToken ct);
}
