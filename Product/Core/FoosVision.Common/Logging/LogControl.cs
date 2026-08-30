// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Common.Logging;

public static class LogControl
{
    private static volatile Severity _Min = Severity.Information;

    public static Severity MinimumSeverity
    {
        get => _Min;
        set => _Min = value;
    }

    public static bool IsVerbose
        => _Min == Severity.Verbose;
}
