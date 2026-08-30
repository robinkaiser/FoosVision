// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.VideoPlayer.Options;
using FoosVision.VideoPlayer.Runtime;

namespace FoosVision.VideoPlayer;

internal static class Program
{
    private static readonly Source _Log = new("VideoPlayer.Program");

    private static async Task<int> Main(string[] args)
    {
        VideoPlayerLoggingBootstrap.Initialize();

        try
        {
            VideoPlayerCommandLineParser parser = new();
            VideoPlayerCommandLineParseResult result = parser.Parse(args);

            if (result.ShowHelp)
            {
                Console.WriteLine(VideoPlayerCommandLineHelp.Text);
                return 0;
            }

            if (!result.IsSuccess)
            {
                Console.Error.WriteLine(result.ErrorMessage);
                Console.Error.WriteLine();
                Console.Error.WriteLine(VideoPlayerCommandLineHelp.Text);
                _Log.Error("Command line parsing failed: {0}", result.ErrorMessage ?? "Unknown error.");
                return 1;
            }

            try
            {
                FfmpegRuntimeBootstrap.EnsureInitialized();
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                _Log.Fatal("FFmpeg runtime bootstrap failed.", ex);
                return 1;
            }

            using CancellationTokenSource cancellation = new();
            ConsoleCancelEventHandler handler = (_, e) =>
            {
                e.Cancel = true;
                _Log.Information("Ctrl+C received. Shutting down VideoPlayer.");
                cancellation.Cancel();
            };

            Console.CancelKeyPress += handler;

            try
            {
                VideoPlayerApplication application = new();
                return await application.RunAsync(result.Options!, cancellation.Token);
            }
            finally
            {
                Console.CancelKeyPress -= handler;
            }
        }
        catch (Exception ex)
        {
            _Log.Fatal("VideoPlayer terminated unexpectedly.", ex);
            throw;
        }
        finally
        {
            VideoPlayerLoggingBootstrap.Shutdown();
        }
    }
}
