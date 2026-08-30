// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Diagnostics;
using FoosVision.Common.Metrics;
using FoosVision.Media.Android.CameraFeed;
using FoosVision.Media.Android.Diagnostics;
using FoosVision.Recorder.App.Runtime;
using FoosVision.Recorder.App.Platforms.Android;
using FoosVision.Recorder.Composition.Diagnostics;
using FoosVision.Settings;
using FoosVision.Settings.Diagnostics;
using AndroidApplication = Android.App.Application;

namespace FoosVision.Recorder.App;

/// <summary>
/// Provides Android-specific runtime wiring for the recorder app.
/// </summary>
public static partial class RecorderPlatformComposition
{
    /// <summary>
    /// Creates the Android recorder runtime factory.
    /// </summary>
    /// <param name="viewModel">View model receiving recorder status updates.</param>
    /// <returns>Android recorder runtime factory.</returns>
    public static partial IRecorderRuntimeFactory CreateRecorderRuntimeFactory(MainViewModel viewModel)
    {
        return new AndroidRecorderRuntimeFactory(AndroidApplication.Context, viewModel);
    }

    private class AndroidRecorderRuntimeFactory : IRecorderRuntimeFactory
    {
        private readonly Android.Content.Context _Context;
        private readonly MainViewModel _ViewModel;

        public AndroidRecorderRuntimeFactory(Android.Content.Context context, MainViewModel viewModel)
        {
            _Context = context;
            _ViewModel = viewModel;
        }

        public IRecorderRuntime Create()
        {
            RecorderSettingsContext? currentSettings = RecorderLoggingBootstrap.CurrentSettings;
            RuntimeMetricsOptions runtimeMetricsOptions = CreateRuntimeMetricsOptions(currentSettings);
            CameraFeed cameraFeed = new(_Context, runtimeMetricsOptions);
            var videoDumpWriter = CreateVideoDumpWriter();
            IDisposable? processNetworkBinding = AndroidWifiProcessNetworkBinding.Bind(_Context);

            bool publishObservations = false;
            bool publishBallDetectionMask = false;

            if (currentSettings != null)
            {
                var debugVisualizations = currentSettings.Settings.Diagnostics.Vision.DebugVisualizations;

                publishObservations = debugVisualizations.ShowObservations;
                publishBallDetectionMask = debugVisualizations.ShowBallDetectionMask;
            }

            try
            {
                RecorderRuntime runtime = new(
                    cameraFeed,
                    _ViewModel,
                    videoDumpWriter,
                    publishObservations,
                    publishBallDetectionMask,
                    runtimeMetricsOptions,
                    processNetworkBinding);

                return runtime;
            }
            catch
            {
                processNetworkBinding?.Dispose();
                throw;
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
                NamePrefix = "Android",
            };
        }

        private static IVideoDumpWriter CreateVideoDumpWriter()
        {
            RecorderSettingsContext? settings = RecorderLoggingBootstrap.CurrentSettings;

            if (settings is null)
            {
                return new NullVideoDumpWriter();
            }

            DiagnosticsVideoSettings video = settings.Settings.Diagnostics.Video;
            VideoDumpFileWriterOptions options = new(
                settings.Paths.Diagnostics.Videos,
                video.Enabled,
                video.RetentionDays,
                video.MaxTotalSizeBytes);

            return new VideoDumpFileWriter(
                options,
                new AndroidEncodedReplaySegmentFileWriter(),
                new VideoDumpFileSystemFileStore());
        }
    }

    private class NullVideoDumpWriter : IVideoDumpWriter
    {
        public Task WriteAsync(VideoDumpRequest request, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
