// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Adapters.Viewer.Connectivity;

public record RecorderDiscoveryCandidate(string RecorderIpAddress, string RecorderAppVersion, int ProtocolVersion);
