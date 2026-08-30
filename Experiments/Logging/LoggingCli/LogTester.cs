// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;

namespace LoggingCli;

public class LogTester
{
    private static readonly Source _Log = new("LogTester");
    private static readonly SourceInterval _LogInterval = new("LogTester_Interval", TimeSpan.FromSeconds(1));

    public LogTester()
    {
    }

    public static void PositionalTest()
    {
        _Log.Information("Hello, World!");

        Thread.Sleep(500);

        _Log.Warning("Ball lost. FrameId={FrameId} AgeMs={AgeMs}", 10UL, 42);

        Thread.Sleep(500);

        _Log.Error("Ball lost. FrameId={FrameId} AgeMs={AgeMs}", 11, 43);

        Thread.Sleep(500);

        _Log.Fatal("Only one arg. FrameId={FrameId}", 12);
        _Log.Fatal("Another arg = {MyArg}", 321);
    }

    public static void IntervalTest()
    {
        for (int i = 0; i < 30; i++)
        {
            _LogInterval.Information("Interval - Number = {number}", i);
            Thread.Sleep(100);
        }
    }

    public static void VerboseTest(string message)
    {
        var msg = $"Verbose Test {message}";
        _Log.Information(msg);
        _Log.Verbose("Very verbose");

        if (LogControl.IsVerbose)
        {
            _Log.Information("Verbose Test: inside IsEnabled(Verbose)");

            for (int i = 0; i < 5; i++)
            {
                _Log.Verbose("Very verbose i = {i}", i);
            }
        }
    }
}
