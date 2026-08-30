// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Settings.Diagnostics;

namespace FoosVision.Logging.UnitTests.Serilog;

public class SerilogLoggerFactoryTests
{
    [Fact]
    public void DescribeSinks_returns_file_for_default_logging()
    {
        DiagnosticsLoggingSettings settings = DiagnosticsLoggingSettings.CreateDefault();

        string result = SerilogLoggerFactory.DescribeSinks(settings);

        Assert.Equal("File", result);
    }

    [Fact]
    public void DescribeSinks_includes_seq_when_enabled()
    {
        DiagnosticsLoggingSettings settings = DiagnosticsLoggingSettings.CreateDefault();
        settings.Seq.Enabled = true;

        string result = SerilogLoggerFactory.DescribeSinks(settings);

        Assert.Equal("File,Seq", result);
    }

    [Fact]
    public void GetMinimumSeverity_returns_lowest_enabled_sink_level()
    {
        DiagnosticsLoggingSettings settings = DiagnosticsLoggingSettings.CreateDefault();
        settings.File.MinimumLevel = "Warning";
        settings.Seq.Enabled = true;
        settings.Seq.MinimumLevel = "Debug";

        Severity result = SerilogLoggerFactory.GetMinimumSeverity(settings);

        Assert.Equal(Severity.Debug, result);
    }

    [Fact]
    public void GetMinimumSeverity_ignores_disabled_sinks()
    {
        DiagnosticsLoggingSettings settings = DiagnosticsLoggingSettings.CreateDefault();
        settings.File.Enabled = false;
        settings.File.MinimumLevel = "Verbose";
        settings.Seq.Enabled = true;
        settings.Seq.MinimumLevel = "Error";

        Severity result = SerilogLoggerFactory.GetMinimumSeverity(settings);

        Assert.Equal(Severity.Error, result);
    }
}
