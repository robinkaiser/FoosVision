// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings.Diagnostics;

public class DiagnosticsVideoSettings
{
    public bool Enabled { get; set; }

    public int RetentionDays { get; set; } = 1;

    public long MaxTotalSizeBytes { get; set; } = 1073741824;

    public static DiagnosticsVideoSettings CreateDefault()
    {
        return new DiagnosticsVideoSettings();
    }

    public void Validate()
    {
        if (RetentionDays < 0)
        {
            throw new InvalidOperationException($"{nameof(RetentionDays)} must be greater than or equal to zero.");
        }

        if (MaxTotalSizeBytes < 1)
        {
            throw new InvalidOperationException($"{nameof(MaxTotalSizeBytes)} must be greater than or equal to one.");
        }
    }
}
