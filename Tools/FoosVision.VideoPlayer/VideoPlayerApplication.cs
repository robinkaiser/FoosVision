// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.VideoPlayer.Options;
using FoosVision.VideoPlayer.Runtime;

namespace FoosVision.VideoPlayer;

public class VideoPlayerApplication
{
    private static readonly Source _Log = new("VideoPlayer.Application");

    private readonly IVideoPlayerRuntimeFactory _RuntimeFactory;

    public VideoPlayerApplication()
        : this(new VideoPlayerRuntimeFactory())
    {
    }

    public VideoPlayerApplication(IVideoPlayerRuntimeFactory runtimeFactory)
    {
        _RuntimeFactory = runtimeFactory;
    }

    public async Task<int> RunAsync(VideoPlayerOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        using IVideoPlayerRuntime runtime = _RuntimeFactory.Create(options);
        runtime.Start();

        _Log.Information(
            "VideoPlayer recorder host started for file '{0}' ({1}, {2}x{3}, encoded {4} fps, decoded {5} fps).",
            options.FilePath,
            options.Codec,
            options.Width,
            options.Height,
            options.EncodedFps,
            options.DecodedFps);
        Console.WriteLine("FoosVision.VideoPlayer recorder host started. Press Ctrl+C to stop.");

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _Log.Information("VideoPlayer shutdown requested.");
        }

        return 0;
    }
}
