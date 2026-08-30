// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings.Diagnostics;

public class DiagnosticsSeqLoggingSettings
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

    public bool Enabled { get; set; }

    public string ServerUrl { get; set; } = "http://192.168.178.50:5341";

    public string ApiKey { get; set; } = string.Empty;

    public string MinimumLevel { get; set; } = "Debug";

    public bool SendTestEventOnStartup { get; set; } = true;

    public static DiagnosticsSeqLoggingSettings CreateDefault()
    {
        return new DiagnosticsSeqLoggingSettings();
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl))
        {
            throw new InvalidOperationException($"{nameof(ServerUrl)} is required.");
        }

        if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"{nameof(ServerUrl)} must be an absolute HTTP or HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(MinimumLevel))
        {
            throw new InvalidOperationException($"{nameof(MinimumLevel)} is required.");
        }

        if (!_SupportedMinimumLevels.Contains(MinimumLevel))
        {
            throw new InvalidOperationException(
                $"{nameof(MinimumLevel)} '{MinimumLevel}' is not supported. Supported values: {string.Join(", ", _SupportedMinimumLevels)}.");
        }

        ApiKey ??= string.Empty;
    }
}
