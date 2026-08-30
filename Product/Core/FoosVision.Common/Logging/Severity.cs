// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Common.Logging;

/// <summary>
/// Adapted from serilog
/// In addition this should be considered best practise:
/// - Per frame logging should be avoided for log level information and higher.
/// - For performance critical verbose and debug logging, level detection should be used.
/// </summary>
public enum Severity
{
    /// <summary>
    /// Tracing information and debugging minutiae; generally only switched on in unusual situations
    /// </summary>
    Verbose = 0,

    /// <summary>
    /// Internal control flow and diagnostic state dumps to facilitate pinpointing of recognised problems
    /// </summary>
    Debug,

    /// <summary>
    /// Events of interest or that have relevance to outside observers; the default enabled minimum logging level
    /// </summary>
    Information,

    /// <summary>
    /// Indicators of possible issues or service/functionality degradation
    /// </summary>
    Warning,

    /// <summary>
    /// Indicating a failure within the application or connected system
    /// </summary>
    Error,

    /// <summary>
    /// Critical errors causing complete failure of the application
    /// </summary>
    Fatal,
}
