// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Content;
using Android.Views;
using Android.Widget;
using FoosVision.Adapters.Viewer.Session;
using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Adapters.Viewer.Session.Playback;
using FoosVision.Common.Metrics;
using FoosVision.Settings;
using FoosVision.Settings.Diagnostics;
using FoosVision.Viewer.App.Runtime;
using FoosVision.Viewer.App.Screen.Stage;
using AndroidRect = Android.Graphics.Rect;

namespace FoosVision.Viewer.App.Platforms.Android.Screen.Stage;

public class StageLayout :
    FrameLayout,
    IViewerScreenRuntime,
    IOverlaySink
{
    private const float _ReferenceAspectRatio = 16f / 9f;
    private readonly OverlayView _OverlayView;
    private readonly SurfaceView _VideoSurfaceView;
    private readonly VideoPlayer _VideoPlayer;

    public StageLayout(Context context)
        : base(context)
    {
        SetBackgroundColor(global::Android.Graphics.Color.Black);
        SetFitsSystemWindows(false);
        SetClipToPadding(false);
        SetPadding(0, 0, 0, 0);

        SurfaceView videoView = new(context)
        {
            LayoutParameters = new FrameLayout.LayoutParams(1, 1),
        };
        AddView(videoView);

        OverlayView overlayView = new(context)
        {
            LayoutParameters = new FrameLayout.LayoutParams(
                LayoutParams.MatchParent,
                LayoutParams.MatchParent),
        };
        AddView(overlayView);

        _VideoSurfaceView = videoView;
        _OverlayView = overlayView;
        _VideoPlayer = new VideoPlayer(videoView, CreateRuntimeMetricsOptions);
        _VideoPlayer.StreamFpsChanged += OnStreamFpsChanged;
        PlaybackController = new PlaybackController(this, _VideoPlayer);
        PlaybackSourceFactory = new RtpPlaybackSourceFactory(new WritableSessionFile(context, "foosvision-stream.sdp"));
    }

    public event Action<double?>? StreamFpsChanged;

    public IOverlaySink OverlaySink => this;

    public IPlaybackController PlaybackController { get; }

    public IPlaybackSourceFactory PlaybackSourceFactory { get; }

    public void UpdateTrackingState(TrackingOverlayState state)
    {
        _OverlayView.Post(() => _OverlayView.UpdateTrackingState(state));
    }

    public void ClearTrackingState()
    {
        _OverlayView.Post(_OverlayView.ClearTrackingState);
    }

    public void UpdateTableState(TableOverlayState state)
    {
        _OverlayView.Post(() => _OverlayView.UpdateTableState(state));
    }

    public void UpdateBallDetectionMaskState(BallDetectionMaskOverlayState state)
    {
        _OverlayView.Post(() => _OverlayView.UpdateBallDetectionMaskState(state));
    }

    public void ClearBallDetectionMaskState()
    {
        _OverlayView.Post(_OverlayView.ClearBallDetectionMaskState);
    }

    public void UpdateSessionUiState(SessionUiState state)
    {
        _OverlayView.Post(() => _OverlayView.UpdateSessionUiState(state));
    }

    public void UpdateOverlayRotation(float rotationDegrees)
    {
        _OverlayView.Post(() => _OverlayView.UpdatePossessionRotation(rotationDegrees));
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        int width = MeasureSpec.GetSize(widthMeasureSpec);
        int height = MeasureSpec.GetSize(heightMeasureSpec);
        AndroidRect videoViewport = CalculateVideoViewport(width, height);

        int videoWidthMeasureSpec = MeasureSpec.MakeMeasureSpec(videoViewport.Width(), MeasureSpecMode.Exactly);
        int videoHeightMeasureSpec = MeasureSpec.MakeMeasureSpec(videoViewport.Height(), MeasureSpecMode.Exactly);
        _VideoSurfaceView.Measure(videoWidthMeasureSpec, videoHeightMeasureSpec);

        int overlayWidthMeasureSpec = MeasureSpec.MakeMeasureSpec(width, MeasureSpecMode.Exactly);
        int overlayHeightMeasureSpec = MeasureSpec.MakeMeasureSpec(height, MeasureSpecMode.Exactly);
        _OverlayView.Measure(overlayWidthMeasureSpec, overlayHeightMeasureSpec);

        SetMeasuredDimension(width, height);
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        int width = right - left;
        int height = bottom - top;
        AndroidRect videoViewport = CalculateVideoViewport(width, height);

        _VideoSurfaceView.Layout(
            videoViewport.Left,
            videoViewport.Top,
            videoViewport.Right,
            videoViewport.Bottom);
        _OverlayView.Layout(0, 0, width, height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _VideoPlayer.StreamFpsChanged -= OnStreamFpsChanged;
            _VideoPlayer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnStreamFpsChanged(double? streamFps)
    {
        StreamFpsChanged?.Invoke(streamFps);
    }

    private static RuntimeMetricsOptions CreateRuntimeMetricsOptions()
    {
        ViewerSettingsContext? settings = ViewerLoggingBootstrap.CurrentSettings;

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

    private static AndroidRect CalculateVideoViewport(int availableWidth, int availableHeight)
    {
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            return new AndroidRect(0, 0, 0, 0);
        }

        float availableAspectRatio = (float)availableWidth / availableHeight;
        int viewportWidth = availableWidth;
        int viewportHeight = availableHeight;

        if (availableAspectRatio >= _ReferenceAspectRatio)
        {
            viewportWidth = (int)MathF.Round(availableHeight * _ReferenceAspectRatio);
        }
        else
        {
            viewportHeight = (int)MathF.Round(availableWidth / _ReferenceAspectRatio);
        }

        return new AndroidRect(0, 0, viewportWidth, viewportHeight);
    }
}
