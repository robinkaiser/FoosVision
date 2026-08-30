// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Reflection;
using FoosVision.Adapters.Recorder.Connectivity;

namespace RecorderCli;

public class VersionProvider : IRecorderVersionProvider, IDisposable
{
    public string GetAppVersion()
    {
        // Prefer informational version; fall back to assembly version.
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
            return info;

        return asm.GetName().Version?.ToString() ?? "0.0.0";
    }

    public void Dispose()
    {
        // no-op (here for symmetry / future expansion)
    }
}
