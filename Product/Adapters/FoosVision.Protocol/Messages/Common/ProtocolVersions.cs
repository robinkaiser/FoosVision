// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Protocol.Messages.Common;

public static class ProtocolVersions
{
    public static int Current => typeof(ProtocolVersions).Assembly.GetName().Version!.Major;
}
