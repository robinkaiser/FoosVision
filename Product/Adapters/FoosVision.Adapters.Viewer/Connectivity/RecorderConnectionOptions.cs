// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Adapters.Viewer.Connectivity;

public record RecorderConnectionOptions(
    TimeSpan GracePeriod,
    TimeSpan PollInterval,
    TimeSpan PerCandidateHandshakeTimeout)
{
    public static RecorderConnectionOptions Default { get; } = new(
        GracePeriod: TimeSpan.FromSeconds(4),
        PollInterval: TimeSpan.FromMilliseconds(200),
        PerCandidateHandshakeTimeout: TimeSpan.FromSeconds(3));
}
