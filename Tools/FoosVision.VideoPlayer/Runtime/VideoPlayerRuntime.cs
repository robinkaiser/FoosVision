// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Diagnostics;
using FoosVision.Common.Logging;
using FoosVision.Common.Metrics;
using FoosVision.Media.Windows.FileCapture;
using FoosVision.Recorder.Composition;
using FoosVision.Settings;
using FoosVision.Settings.Diagnostics;
using FoosVision.VideoPlayer.Options;

namespace FoosVision.VideoPlayer.Runtime;

public class VideoPlayerRuntime : IVideoPlayerRuntime
{
    private static readonly Source _Log = new("VideoPlayer.Runtime");

    private readonly FileCameraFeed _CameraFeed;
    private readonly RecorderHost _RecorderHost;
    private int _PlaybackCompletionHandled;

    public VideoPlayerRuntime(VideoPlayerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _CameraFeed = new FileCameraFeed(options.ToFileCameraFeedOptions());
        _CameraFeed.PlaybackCompleted += OnPlaybackCompleted;
        bool publishObservations = VideoPlayerLoggingBootstrap.CurrentSettings?.Settings.Diagnostics.Vision.DebugVisualizations.ShowObservations ?? false;
        bool publishBallDetectionMask = VideoPlayerLoggingBootstrap.CurrentSettings?.Settings.Diagnostics.Vision.DebugVisualizations.ShowBallDetectionMask ?? false;
        RuntimeMetricsOptions runtimeMetricsOptions = CreateRuntimeMetricsOptions(VideoPlayerLoggingBootstrap.CurrentSettings);

        _RecorderHost = new RecorderHost(
            _CameraFeed,
            new AssemblyRecorderVersionProvider(),
            new VideoPlayerHandshakeDiagnosticsProvider(),
            new VideoPlayerHandshakeViewerSettingsProvider(),
            new NullVideoDumpWriter(),
            publishObservations,
            publishBallDetectionMask,
            runtimeMetricsOptions);
    }

    public void Start()
    {
        _Log.Information("Starting recorder host.");
        _RecorderHost.Start();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _CameraFeed.PlaybackCompleted -= OnPlaybackCompleted;
        _Log.Information("Disposing VideoPlayer runtime.");
        _RecorderHost.Dispose();
        _CameraFeed.Dispose();
    }

    private void OnPlaybackCompleted()
    {
        if (Interlocked.Exchange(ref _PlaybackCompletionHandled, 1) != 0)
        {
            return;
        }

        _Log.Information("Playback completion received from FileCameraFeed.");
        _ = StopActiveSessionsAfterPlaybackCompletedAsync();
    }

    private async Task StopActiveSessionsAfterPlaybackCompletedAsync()
    {
        try
        {
            await _RecorderHost.StopActiveSessions(CancellationToken.None);
            _Log.Information("EOF triggered StopActiveSessions via recorder composition.");
        }
        catch (Exception ex)
        {
            _Log.Error("Failed to stop active sessions on playback completion.", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _PlaybackCompletionHandled, 0);
        }
    }

    private static RuntimeMetricsOptions CreateRuntimeMetricsOptions(RecorderSettingsContext? settings)
    {
        if (settings is null)
        {
            return RuntimeMetricsOptions.CreateDefault();
        }

        DiagnosticsRuntimeMetricsSettings runtimeMetrics = settings.Settings.Diagnostics.RuntimeMetrics;

        return new RuntimeMetricsOptions
        {
            Enabled = runtimeMetrics.Enabled,
            ReportInterval = TimeSpan.FromSeconds(Math.Max(1, runtimeMetrics.ReportIntervalSeconds)),
            NamePrefix = "VideoPlayer",
        };
    }

    private class NullVideoDumpWriter : IVideoDumpWriter
    {
        public Task WriteAsync(VideoDumpRequest request, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
