// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Adapters.Viewer.Connectivity;

public interface IRecorderDiscovery
{
    IRecorderDiscoverySession Start();
}

public interface IRecorderDiscoverySession : IDisposable
{
    IReadOnlyList<RecorderDiscoveryCandidate> GetCandidatesRankedSnapshot();
}
