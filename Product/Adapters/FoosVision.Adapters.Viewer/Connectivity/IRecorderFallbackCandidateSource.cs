// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Adapters.Viewer.Connectivity;

public interface IRecorderFallbackCandidateSource
{
    Task<IReadOnlyList<RecorderDiscoveryCandidate>> GetCandidatesAsync(CancellationToken ct);
}
