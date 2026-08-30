// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Logging;
using LoggingCli;
using Serilog;

// Download Seq: https://datalust.co/download
// - Logging must be activated in Seq first before any Logs are shown -> click "Tail" next to the Play button
// - How to add App as column: open any event. Click green tick (first icon in row), then "Show as column"

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose() // Log everything, log level is controlled by FoosVision.Common.Logging.LogControl
    .Enrich.WithProperty("App", "Recorder")

    // Console log slows down a lot (2 ms per log!)
    //.WriteTo.Console()
    .WriteTo.Seq(
        "http://127.0.0.1:5341",
        batchPostingLimit: 50,
        period: TimeSpan.FromMilliseconds(500))
    .CreateLogger();

var sink = new SerilogSink(Log.Logger);

Logger.Bind(sink);

LogTester.PositionalTest();
LogTester.IntervalTest();
LogTester.VerboseTest("Verbose Off");

LogControl.MinimumSeverity = Severity.Verbose;

LogTester.VerboseTest("Verbose On");

Log.CloseAndFlush();
