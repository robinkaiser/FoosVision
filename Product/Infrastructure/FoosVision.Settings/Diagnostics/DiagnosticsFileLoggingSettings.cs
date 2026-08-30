// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings.Diagnostics;

public class DiagnosticsFileLoggingSettings
{
    private static readonly HashSet<string> _SupportedMinimumLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Verbose",
        "Debug",
        "Information",
        "Warning",
        "Error",
        "Fatal",
    };

    private static readonly HashSet<string> _SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "CompactJson",
        "Text",
    };

    private static readonly HashSet<string> _SupportedRollingIntervals = new(StringComparer.OrdinalIgnoreCase)
    {
        "Infinite",
        "Year",
        "Month",
        "Day",
        "Hour",
        "Minute",
    };

    public bool Enabled { get; set; } = true;

    public string MinimumLevel { get; set; } = "Information";

    public string Format { get; set; } = "CompactJson";

    public string RollingInterval { get; set; } = "Day";

    public int RetentionDays { get; set; } = 7;

    public int RetainedFileCountLimit { get; set; } = 7;

    public static DiagnosticsFileLoggingSettings CreateDefault()
    {
        return new DiagnosticsFileLoggingSettings();
    }

    public void Validate()
    {
        ValidateRequiredOption(nameof(MinimumLevel), MinimumLevel, _SupportedMinimumLevels);
        ValidateRequiredOption(nameof(Format), Format, _SupportedFormats);
        ValidateRequiredOption(nameof(RollingInterval), RollingInterval, _SupportedRollingIntervals);

        if (RetentionDays < 0)
        {
            throw new InvalidOperationException($"{nameof(RetentionDays)} must be greater than or equal to zero.");
        }

        if (RetainedFileCountLimit < 0)
        {
            throw new InvalidOperationException($"{nameof(RetainedFileCountLimit)} must be greater than or equal to zero.");
        }
    }

    private static void ValidateRequiredOption(string name, string? value, HashSet<string> supportedValues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{name} is required.");
        }

        if (!supportedValues.Contains(value))
        {
            throw new InvalidOperationException(
                $"{name} '{value}' is not supported. Supported values: {string.Join(", ", supportedValues)}.");
        }
    }
}
