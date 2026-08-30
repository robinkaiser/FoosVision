// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Protocol.Connectivity.Discovery;

public readonly record struct RecorderIdentity(int ProtocolVersion, string AppVersion);

/// <summary>
/// Shared identity format for UDP discovery (NetDiscovery).
/// Format: "FoosVisionRecorder|proto=1|app=1.2.3".
/// </summary>
public static class DiscoveryIdentity
{
    private const string _RecorderPrefix = "FoosVisionRecorder";
    private const char _Sep = '|';

    public static string BuildRecorderIdentity(int protocolVersion, string appVersion)
    {
        return $"{_RecorderPrefix}{_Sep}proto={protocolVersion}{_Sep}app={appVersion}";
    }

    public static string DescribeRecorderSearchIdentity(int protocolVersion)
    {
        return $"{_RecorderPrefix}{_Sep}proto={protocolVersion}{_Sep}app=*";
    }

    public static bool IsRecorderIdentity(string? identity)
        => identity is not null &&
           identity.StartsWith(_RecorderPrefix + _Sep, StringComparison.Ordinal);

    public static bool TryParseRecorderIdentity(string? identity, out RecorderIdentity parsed)
    {
        parsed = default;

        if (!IsRecorderIdentity(identity)) return false;
        if (identity is null) return false;

        int? proto = null;
        string app = string.Empty;

        // Split by '|' and parse key=value pairs after the prefix token.
        var parts = identity.Split(_Sep, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false; // must contain at least one key=value

        for (var i = 1; i < parts.Length; i++)
        {
            var kv = parts[i];
            var eq = kv.IndexOf('=');
            if (eq <= 0 || eq == kv.Length - 1) continue;

            var key = kv.Substring(0, eq);
            var value = kv.Substring(eq + 1);

            switch (key)
            {
                case "proto":
                    if (int.TryParse(value, out var v) && v > 0) proto = v;
                    break;

                case "app":
                    app = value;
                    break;
            }
        }

        if (proto is null) return false;

        parsed = new RecorderIdentity(proto.Value, app);

        return true;
    }
}
