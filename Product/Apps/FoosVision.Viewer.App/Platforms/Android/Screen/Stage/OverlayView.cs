// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Content;
using Android.Graphics;
using Android.OS;
using FoosVision.Adapters.Viewer.Session;
using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Protocol.Messages.Live;
using AndroidGraphics = Android.Graphics;
using AndroidView = Android.Views.View;

namespace FoosVision.Viewer.App.Platforms.Android.Screen.Stage;

/// <summary>
/// Minimal transparent overlay used to validate native overlay composition above video playback.
/// </summary>
public class OverlayView : AndroidView
{
    private const float _ReferenceAspectRatio = 16f / 9f;
    private const int _BallDetectionMaskColor = unchecked((int)0xFFFF00FF);
    private const long _ModeBlinkIntervalMs = 500;
    private const int _ModeBlinkToggles = 3;
    private const int _PossessionAreaAlpha = 40;
    private static readonly SessionUiState _InitialSessionUiState = new(SessionMode.Install, false, false, true, false);
    private readonly AndroidGraphics.Paint _BallPaint;
    private readonly AndroidGraphics.Paint _BallDetectionMaskPaint;
    private readonly AndroidGraphics.Paint _BarCenterPaint;
    private readonly AndroidGraphics.Paint _FramePaint;
    private readonly AndroidGraphics.Paint _ModeChipAccentPaint;
    private readonly AndroidGraphics.Paint _ModeChipBackgroundPaint;
    private readonly AndroidGraphics.Paint _ModeChipTextPaint;
    private readonly AndroidGraphics.Paint _MetricBackgroundPaint;
    private readonly AndroidGraphics.Paint _MetricTextPaint;
    private readonly AndroidGraphics.Paint _ObservationPaint;
    private readonly AndroidGraphics.Paint _PossessionAreaPaint;
    private readonly AndroidGraphics.Paint _PossessionBackgroundPaint;
    private readonly AndroidGraphics.Paint _PossessionTextPaint;
    private readonly AndroidGraphics.Paint _BallCandidatePaint;
    private readonly AndroidGraphics.Paint _TableBoundaryPaint;
    private readonly AndroidGraphics.Paint _TrailPaint;
    private readonly AndroidGraphics.Paint _ViewerStatusAccentPaint;
    private readonly AndroidGraphics.Paint _ViewerStatusBackgroundPaint;
    private readonly AndroidGraphics.Paint _ViewerStatusPrimaryTextPaint;
    private readonly AndroidGraphics.Paint _ViewerStatusSecondaryTextPaint;
    private readonly string _ViewerTitle;
    private float _PossessionRotationDegrees;
    private AndroidGraphics.Bitmap? _BallDetectionMaskBitmap;
    private int[] _BallDetectionMaskPixels = [];
    private VideoDisplayMode _DisplayMode;
    private long _ModeBlinkStartedMs;
    private bool _HasSessionStartRequested;
    private SessionUiState _SessionUiState = _InitialSessionUiState;
    private TableOverlayState? _TableState;
    private TrackingOverlayState? _TrackingState;

    public OverlayView(Context context)
        : base(context)
    {
        _ViewerTitle = CreateViewerTitle();

        _FramePaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(170, 104, 114, 128),
            StrokeWidth = 3f,
        };
        _FramePaint.SetStyle(AndroidGraphics.Paint.Style.Stroke);

        _TableBoundaryPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(255, 255, 235, 59),
            StrokeWidth = 9f,
        };
        _TableBoundaryPaint.SetStyle(AndroidGraphics.Paint.Style.Stroke);
        _TableBoundaryPaint.StrokeJoin = AndroidGraphics.Paint.Join.Round;

        _BarCenterPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(255, 255, 235, 59),
            StrokeWidth = 9f,
        };
        _BarCenterPaint.SetStyle(AndroidGraphics.Paint.Style.Stroke);
        _BarCenterPaint.StrokeCap = AndroidGraphics.Paint.Cap.Round;

        _PossessionAreaPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(_PossessionAreaAlpha, 255, 255, 255),
        };
        _PossessionAreaPaint.SetStyle(AndroidGraphics.Paint.Style.Fill);

        _BallPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(240, 255, 255, 255),
        };
        _BallPaint.SetStyle(AndroidGraphics.Paint.Style.Fill);

        _BallDetectionMaskPaint = new AndroidGraphics.Paint()
        {
            FilterBitmap = false,
            Dither = false,
        };

        _ObservationPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(240, 234, 67, 53),
        };
        _ObservationPaint.SetStyle(AndroidGraphics.Paint.Style.Stroke);

        _BallCandidatePaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(210, 255, 179, 71),
            StrokeWidth = 5f,
        };
        _BallCandidatePaint.SetStyle(AndroidGraphics.Paint.Style.Stroke);

        _TrailPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(170, 255, 179, 71),
            StrokeWidth = 6f,
        };
        _TrailPaint.SetStyle(AndroidGraphics.Paint.Style.Stroke);
        _TrailPaint.StrokeCap = AndroidGraphics.Paint.Cap.Round;
        _TrailPaint.StrokeJoin = AndroidGraphics.Paint.Join.Round;

        _MetricBackgroundPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(220, 22, 28, 36),
        };
        _MetricBackgroundPaint.SetStyle(AndroidGraphics.Paint.Style.Fill);

        _MetricTextPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(255, 255, 255, 255),
            TextAlign = AndroidGraphics.Paint.Align.Left,
        };
        _MetricTextPaint.SetStyle(AndroidGraphics.Paint.Style.Fill);

        _ModeChipBackgroundPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(220, 22, 28, 36),
        };
        _ModeChipBackgroundPaint.SetStyle(AndroidGraphics.Paint.Style.Fill);

        _ModeChipAccentPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias);
        _ModeChipAccentPaint.SetStyle(AndroidGraphics.Paint.Style.Fill);

        _ModeChipTextPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(255, 255, 255, 255),
            TextAlign = AndroidGraphics.Paint.Align.Left,
        };
        _ModeChipTextPaint.SetStyle(AndroidGraphics.Paint.Style.Fill);

        _PossessionBackgroundPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(235, 70, 70, 70),
        };
        _PossessionBackgroundPaint.SetStyle(AndroidGraphics.Paint.Style.Fill);

        _PossessionTextPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(255, 255, 235, 59),
            TextAlign = AndroidGraphics.Paint.Align.Center,
        };
        _PossessionTextPaint.SetStyle(AndroidGraphics.Paint.Style.Fill);

        _ViewerStatusBackgroundPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(220, 22, 28, 36),
        };
        _ViewerStatusBackgroundPaint.SetStyle(AndroidGraphics.Paint.Style.Fill);

        _ViewerStatusAccentPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias);
        _ViewerStatusAccentPaint.SetStyle(AndroidGraphics.Paint.Style.Fill);

        _ViewerStatusPrimaryTextPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(255, 255, 255, 255),
            TextAlign = AndroidGraphics.Paint.Align.Left,
        };
        _ViewerStatusPrimaryTextPaint.SetStyle(AndroidGraphics.Paint.Style.Fill);

        _ViewerStatusSecondaryTextPaint = new AndroidGraphics.Paint(PaintFlags.AntiAlias)
        {
            Color = AndroidGraphics.Color.Argb(255, 198, 207, 218),
            TextAlign = AndroidGraphics.Paint.Align.Left,
        };
        _ViewerStatusSecondaryTextPaint.SetStyle(AndroidGraphics.Paint.Style.Fill);

        Clickable = false;
        Focusable = false;
    }

    public void UpdateTrackingState(TrackingOverlayState state)
    {
        _TrackingState = state;
        Invalidate();
    }

    public void ClearTrackingState()
    {
        _TrackingState = null;
        Invalidate();
    }

    public void UpdateTableState(TableOverlayState state)
    {
        _TableState = state;
        Invalidate();
    }

    public void UpdateBallDetectionMaskState(BallDetectionMaskOverlayState state)
    {
        if (state.Width <= 0 ||
            state.Height <= 0 ||
            state.Width > int.MaxValue / state.Height ||
            state.Length < 0 ||
            state.Length > state.Buffer.Length)
        {
            return;
        }

        int pixelCount = state.Width * state.Height;
        if (_BallDetectionMaskPixels.Length < pixelCount)
        {
            _BallDetectionMaskPixels = new int[pixelCount];
        }

        if (state.Buffer.Length < pixelCount ||
            state.Length < pixelCount)
        {
            return;
        }

        WriteBallDetectionMaskPixels(state.Buffer, pixelCount, _BallDetectionMaskPixels);

        if (_BallDetectionMaskBitmap is null ||
            _BallDetectionMaskBitmap.Width != state.Width ||
            _BallDetectionMaskBitmap.Height != state.Height)
        {
            _BallDetectionMaskBitmap?.Dispose();
            AndroidGraphics.Bitmap.Config bitmapConfig = AndroidGraphics.Bitmap.Config.Argb8888
                ?? throw new InvalidOperationException("ARGB8888 bitmap config is not available.");
            _BallDetectionMaskBitmap = AndroidGraphics.Bitmap.CreateBitmap(
                state.Width,
                state.Height,
                bitmapConfig);
        }

        _BallDetectionMaskBitmap.SetPixels(_BallDetectionMaskPixels, 0, state.Width, 0, 0, state.Width, state.Height);
        Invalidate();
    }

    public void ClearBallDetectionMaskState()
    {
        _BallDetectionMaskBitmap?.Dispose();
        _BallDetectionMaskBitmap = null;
        Invalidate();
    }

    public void UpdateSessionUiState(SessionUiState state)
    {
        VideoDisplayMode displayMode = GetDisplayMode(state);
        if (_DisplayMode == displayMode && _SessionUiState == state)
        {
            return;
        }

        if (state.IsConnected && (state.IsPendingCommand || state.IsRunning))
        {
            _HasSessionStartRequested = true;
        }

        _SessionUiState = state;
        if (_DisplayMode == displayMode)
        {
            Invalidate();
            return;
        }

        _DisplayMode = displayMode;
        _ModeBlinkStartedMs = displayMode == VideoDisplayMode.None
            ? 0L
            : SystemClock.UptimeMillis();
        Invalidate();
    }

    public void UpdatePossessionRotation(float rotationDegrees)
    {
        if (Math.Abs(_PossessionRotationDegrees - rotationDegrees) < 0.1f)
        {
            return;
        }

        _PossessionRotationDegrees = rotationDegrees;
        Invalidate();
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);

        float width = Width;
        float height = Height;
        if (width <= 0f || height <= 0f)
        {
            return;
        }

        AndroidGraphics.RectF videoViewport = CalculateVideoViewport(width, height);
        float viewportScale = MathF.Min(videoViewport.Width(), videoViewport.Height());
        float frameStrokeWidth = MathF.Max(2f, viewportScale * 0.0032f);

        _FramePaint.StrokeWidth = frameStrokeWidth;
        canvas.DrawRect(videoViewport, _FramePaint);
        DrawBallDetectionMask(canvas, videoViewport);
        DrawPossessionArea(canvas, videoViewport);
        DrawTable(canvas, videoViewport, viewportScale);
        DrawTrail(canvas, videoViewport, viewportScale);
        DrawObservations(canvas, videoViewport, viewportScale);
        DrawBallCandidates(canvas, videoViewport, viewportScale);
        DrawBall(canvas, videoViewport, viewportScale);
        DrawPossessionTime(canvas, videoViewport, viewportScale);
        DrawMetrics(canvas, videoViewport, viewportScale);
        DrawViewerStatus(canvas, videoViewport, viewportScale);
        DrawModeChip(canvas, videoViewport, viewportScale);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _BallDetectionMaskBitmap?.Dispose();
            _BallDetectionMaskBitmap = null;
        }

        base.Dispose(disposing);
    }

    private void DrawBallDetectionMask(Canvas canvas, AndroidGraphics.RectF viewport)
    {
        if (_BallDetectionMaskBitmap is null)
        {
            return;
        }

        canvas.DrawBitmap(_BallDetectionMaskBitmap, null, viewport, _BallDetectionMaskPaint);
    }

    private void DrawPossessionArea(Canvas canvas, AndroidGraphics.RectF viewport)
    {
        if (_DisplayMode != VideoDisplayMode.Live || _TrackingState is null || _TableState is null)
        {
            return;
        }

        TableBarOverlay? possessionBar = GetPossessionBar(_TrackingState, _TableState);
        if (possessionBar is null)
        {
            return;
        }

        TrapeziumOverlay? possessionArea = CreatePossessionArea(_TrackingState, _TableState);
        if (possessionArea is null)
        {
            return;
        }

        _PossessionAreaPaint.Color = CreateColorWithAlpha(possessionBar.TeamArgb, _PossessionAreaAlpha);
        DrawPolygon(
            canvas,
            viewport,
            _PossessionAreaPaint,
            possessionArea.UpperLeft,
            possessionArea.UpperRight,
            possessionArea.LowerRight,
            possessionArea.LowerLeft);
    }

    private void DrawTable(Canvas canvas, AndroidGraphics.RectF viewport, float viewportScale)
    {
        if (_TableState is null || _TableState.Bars.Count == 0)
        {
            return;
        }

        float frameStrokeWidth = MathF.Max(2f, viewportScale * 0.0032f);
        float tableStrokeWidth = frameStrokeWidth * 2f;
        _TableBoundaryPaint.StrokeWidth = tableStrokeWidth;
        _BarCenterPaint.StrokeWidth = tableStrokeWidth;

        DrawPolygon(
            canvas,
            viewport,
            _TableBoundaryPaint,
            _TableState.Boundary.UpperLeft,
            _TableState.Boundary.UpperRight,
            _TableState.Boundary.LowerRight,
            _TableState.Boundary.LowerLeft);

        foreach (TrapeziumOverlay occlusion in _TableState.Occlusions)
        {
            DrawPolygon(
                canvas,
                viewport,
                _TableBoundaryPaint,
                occlusion.UpperLeft,
                occlusion.UpperRight,
                occlusion.LowerRight,
                occlusion.LowerLeft);
        }

        for (int i = 0; i < _TableState.Bars.Count; i++)
        {
            TableBarOverlay bar = _TableState.Bars[i];
            _BarCenterPaint.Color = CreateColor(bar.TeamArgb);
            DrawLine(canvas, viewport, _BarCenterPaint, bar.Center);
        }
    }

    private void DrawTrail(Canvas canvas, AndroidGraphics.RectF viewport, float viewportScale)
    {
        if (_TrackingState is null || _TrackingState.Trail.Count < 2)
        {
            return;
        }

        _TrailPaint.StrokeWidth = MathF.Max(4f, viewportScale * 0.0055f);

        AndroidGraphics.Path trailPath = new();
        (float firstX, float firstY) = ToCanvas(_TrackingState.Trail[0], viewport);
        trailPath.MoveTo(firstX, firstY);

        for (int i = 1; i < _TrackingState.Trail.Count; i++)
        {
            (float pointX, float pointY) = ToCanvas(_TrackingState.Trail[i], viewport);
            trailPath.LineTo(pointX, pointY);
        }

        canvas.DrawPath(trailPath, _TrailPaint);
    }

    private void DrawBall(Canvas canvas, AndroidGraphics.RectF viewport, float viewportScale)
    {
        if (_TrackingState?.BallPosition is not OverlayPoint ballPosition)
        {
            return;
        }

        (float x, float y) = ToCanvas(ballPosition, viewport);
        float radius = MathF.Max(10f, viewportScale * 0.011f);
        canvas.DrawCircle(x, y, radius, _BallPaint);
    }

    private void DrawBallCandidates(Canvas canvas, AndroidGraphics.RectF viewport, float viewportScale)
    {
        if (_TrackingState is null || _TrackingState.BallCandidates.Count == 0)
        {
            return;
        }

        float radius = MathF.Max(10f, viewportScale * 0.011f);

        foreach (BallCandidateOverlayPoint candidate in _TrackingState.BallCandidates)
        {
            (float x, float y) = ToCanvas(candidate.Position, viewport);
            _BallCandidatePaint.Color = GetCandidateColor(candidate.Status);

            if (candidate.Confidence == TrackingConfidence.High)
            {
                _BallCandidatePaint.SetStyle(AndroidGraphics.Paint.Style.Fill);
            }
            else
            {
                _BallCandidatePaint.SetStyle(AndroidGraphics.Paint.Style.Stroke);
                _BallCandidatePaint.StrokeWidth = GetCandidateStrokeWidth(candidate.Confidence, viewportScale);
            }

            canvas.DrawCircle(x, y, radius, _BallCandidatePaint);
        }
    }

    private void DrawObservations(Canvas canvas, AndroidGraphics.RectF viewport, float viewportScale)
    {
        if (_TrackingState is null || _TrackingState.Observations.Count == 0)
        {
            return;
        }

        float radius = MathF.Max(10f, viewportScale * 0.011f) * 2f;

        foreach (ObservationOverlayPoint observation in _TrackingState.Observations)
        {
            (float x, float y) = ToCanvas(observation.Position, viewport);
            (AndroidGraphics.Color color, float strokeWidth) = GetObservationStyle(observation.QualityLevel, viewportScale);
            _ObservationPaint.Color = color;
            _ObservationPaint.StrokeWidth = strokeWidth;
            canvas.DrawCircle(x, y, radius, _ObservationPaint);
        }
    }

    private void DrawPossessionTime(Canvas canvas, AndroidGraphics.RectF viewport, float viewportScale)
    {
        if (_TrackingState is null || _TableState is null)
        {
            return;
        }

        TableBarOverlay? possessionBar = GetPossessionBar(_TrackingState, _TableState);
        if (possessionBar is null)
        {
            return;
        }

        float textSize = MathF.Max(26f, viewportScale * 0.032f);
        float horizontalPadding = MathF.Max(14f, viewportScale * 0.014f);
        float verticalPadding = MathF.Max(8f, viewportScale * 0.008f);
        float radius = MathF.Max(10f, viewportScale * 0.010f);
        float marginTop = MathF.Max(10f, viewportScale * 0.012f);
        float centerX = viewport.Left + (GetBarTopX(possessionBar) * viewport.Width());
        string text = FormatPossessionTime(_TrackingState.PossessionTimeMs);

        _PossessionTextPaint.TextSize = textSize;
        _PossessionBackgroundPaint.Color = _TrackingState.IsTimeFoul
            ? AndroidGraphics.Color.Argb(235, 198, 40, 40)
            : AndroidGraphics.Color.Argb(235, 70, 70, 70);

        float textWidth = _PossessionTextPaint.MeasureText(text);
        AndroidGraphics.Paint.FontMetrics? fontMetrics = _PossessionTextPaint.GetFontMetrics();
        if (fontMetrics is null)
        {
            return;
        }

        float textHeight = fontMetrics.Descent - fontMetrics.Ascent;
        float boxWidth = textWidth + (horizontalPadding * 2f);
        float boxHeight = textHeight + (verticalPadding * 2f);
        float boxLeft = Math.Max(viewport.Left, centerX - (boxWidth * 0.5f));
        float boxRight = Math.Min(viewport.Right, boxLeft + boxWidth);
        if (boxRight - boxLeft < boxWidth)
        {
            boxLeft = boxRight - boxWidth;
        }

        float boxTop = viewport.Top + marginTop;
        float boxBottom = boxTop + boxHeight;
        AndroidGraphics.RectF backgroundRect = new(boxLeft, boxTop, boxRight, boxBottom);
        float textBaseline = boxTop + verticalPadding - fontMetrics.Ascent;

        canvas.Save();
        canvas.Rotate(_PossessionRotationDegrees, backgroundRect.CenterX(), backgroundRect.CenterY());
        canvas.DrawRoundRect(backgroundRect, radius, radius, _PossessionBackgroundPaint);
        canvas.DrawText(text, backgroundRect.CenterX(), textBaseline, _PossessionTextPaint);
        canvas.Restore();
    }

    private void DrawMetrics(Canvas canvas, AndroidGraphics.RectF viewport, float viewportScale)
    {
        if (_TrackingState is null || _TrackingState.Metrics.Count == 0)
        {
            return;
        }

        float textSize = MathF.Max(24f, viewportScale * 0.028f);
        float horizontalPadding = MathF.Max(14f, viewportScale * 0.014f);
        float verticalPadding = MathF.Max(8f, viewportScale * 0.008f);
        float lineGap = MathF.Max(4f, viewportScale * 0.004f);
        float radius = MathF.Max(8f, viewportScale * 0.008f);
        float margin = MathF.Max(12f, viewportScale * 0.014f);

        _MetricTextPaint.TextSize = textSize;
        AndroidGraphics.Paint.FontMetrics? fontMetrics = _MetricTextPaint.GetFontMetrics();
        if (fontMetrics is null)
        {
            return;
        }

        string[] lines = [.. _TrackingState.Metrics.Select(FormatMetric)];
        float lineHeight = fontMetrics.Descent - fontMetrics.Ascent;
        float textWidth = lines.Max(line => _MetricTextPaint.MeasureText(line));
        float boxWidth = textWidth + (horizontalPadding * 2f);
        float boxHeight = (lineHeight * lines.Length) + (lineGap * Math.Max(0, lines.Length - 1)) + (verticalPadding * 2f);
        float boxLeft = viewport.Left + margin;
        float boxTop = viewport.Top + margin;
        AndroidGraphics.RectF backgroundRect = new(boxLeft, boxTop, boxLeft + boxWidth, boxTop + boxHeight);

        canvas.DrawRoundRect(backgroundRect, radius, radius, _MetricBackgroundPaint);

        float baseline = boxTop + verticalPadding - fontMetrics.Ascent;
        for (int i = 0; i < lines.Length; i++)
        {
            canvas.DrawText(lines[i], boxLeft + horizontalPadding, baseline, _MetricTextPaint);
            baseline += lineHeight + lineGap;
        }
    }

    private void DrawModeChip(Canvas canvas, AndroidGraphics.RectF viewport, float viewportScale)
    {
        if (_DisplayMode == VideoDisplayMode.None)
        {
            return;
        }

        string text = GetDisplayModeText(_DisplayMode);
        float textSize = MathF.Max(24f, viewportScale * 0.028f);
        float horizontalPadding = MathF.Max(14f, viewportScale * 0.014f);
        float verticalPadding = MathF.Max(8f, viewportScale * 0.008f);
        float radius = MathF.Max(8f, viewportScale * 0.008f);
        float margin = MathF.Max(12f, viewportScale * 0.014f);
        float accentRadius = MathF.Max(5f, viewportScale * 0.005f);
        float accentGap = MathF.Max(8f, viewportScale * 0.008f);

        _ModeChipTextPaint.TextSize = textSize;
        AndroidGraphics.Paint.FontMetrics? fontMetrics = _ModeChipTextPaint.GetFontMetrics();
        if (fontMetrics is null)
        {
            return;
        }

        float lineHeight = fontMetrics.Descent - fontMetrics.Ascent;
        float textWidth = _ModeChipTextPaint.MeasureText(text);
        float boxWidth = textWidth + accentRadius + accentGap + (horizontalPadding * 2f);
        float boxHeight = lineHeight + (verticalPadding * 2f);
        float boxRight = viewport.Right - margin;
        float boxLeft = boxRight - boxWidth;
        float boxTop = viewport.Top + margin;
        AndroidGraphics.RectF backgroundRect = new(boxLeft, boxTop, boxRight, boxTop + boxHeight);

        _ModeChipAccentPaint.Color = IsModeAccentVisible()
            ? GetDisplayModeAccentColor(_DisplayMode)
            : AndroidGraphics.Color.Transparent;
        canvas.DrawRoundRect(backgroundRect, radius, radius, _ModeChipBackgroundPaint);
        canvas.DrawCircle(
            boxLeft + horizontalPadding + accentRadius,
            backgroundRect.CenterY(),
            accentRadius,
            _ModeChipAccentPaint);

        float textX = boxLeft + horizontalPadding + (accentRadius * 2f) + accentGap;
        float baseline = boxTop + verticalPadding - fontMetrics.Ascent;
        canvas.DrawText(text, textX, baseline, _ModeChipTextPaint);

        if (IsModeBlinkActive())
        {
            PostInvalidateDelayed(_ModeBlinkIntervalMs);
        }
    }

    private void DrawViewerStatus(Canvas canvas, AndroidGraphics.RectF viewport, float viewportScale)
    {
        if (_HasSessionStartRequested)
        {
            return;
        }

        string title = _ViewerTitle;
        string status = GetViewerStatusText(_SessionUiState);
        float titleTextSize = MathF.Max(24f, viewportScale * 0.028f);
        float statusTextSize = MathF.Max(18f, viewportScale * 0.021f);
        float horizontalPadding = MathF.Max(14f, viewportScale * 0.014f);
        float verticalPadding = MathF.Max(10f, viewportScale * 0.010f);
        float lineGap = MathF.Max(4f, viewportScale * 0.004f);
        float radius = MathF.Max(8f, viewportScale * 0.008f);
        float margin = MathF.Max(12f, viewportScale * 0.014f);
        float accentRadius = MathF.Max(5f, viewportScale * 0.005f);
        float accentGap = MathF.Max(8f, viewportScale * 0.008f);
        float maxBoxWidth = MathF.Max(0f, viewport.Width() - (margin * 2f));

        SetViewerStatusTextSizes(title, status, maxBoxWidth, horizontalPadding, accentRadius, accentGap, ref titleTextSize, ref statusTextSize);

        AndroidGraphics.Paint.FontMetrics? titleFontMetrics = _ViewerStatusPrimaryTextPaint.GetFontMetrics();
        AndroidGraphics.Paint.FontMetrics? statusFontMetrics = _ViewerStatusSecondaryTextPaint.GetFontMetrics();
        if (titleFontMetrics is null || statusFontMetrics is null)
        {
            return;
        }

        float titleLineHeight = titleFontMetrics.Descent - titleFontMetrics.Ascent;
        float statusLineHeight = statusFontMetrics.Descent - statusFontMetrics.Ascent;
        float titleWidth = _ViewerStatusPrimaryTextPaint.MeasureText(title);
        float statusWidth = _ViewerStatusSecondaryTextPaint.MeasureText(status);
        float statusRowWidth = (accentRadius * 2f) + accentGap + statusWidth;
        float boxWidth = MathF.Min(
            maxBoxWidth,
            MathF.Max(titleWidth, statusRowWidth) + (horizontalPadding * 2f));
        float boxHeight = titleLineHeight + lineGap + statusLineHeight + (verticalPadding * 2f);
        float boxLeft = viewport.Left + margin;
        float boxTop = viewport.Top + margin;
        AndroidGraphics.RectF backgroundRect = new(boxLeft, boxTop, boxLeft + boxWidth, boxTop + boxHeight);

        _ViewerStatusAccentPaint.Color = GetViewerStatusAccentColor(_SessionUiState);

        canvas.DrawRoundRect(backgroundRect, radius, radius, _ViewerStatusBackgroundPaint);

        float titleBaseline = boxTop + verticalPadding - titleFontMetrics.Ascent;
        float statusBaseline = titleBaseline + titleLineHeight + lineGap;
        float statusCenterY = statusBaseline + ((statusFontMetrics.Ascent + statusFontMetrics.Descent) * 0.5f);
        float textLeft = boxLeft + horizontalPadding;

        canvas.DrawText(title, textLeft, titleBaseline, _ViewerStatusPrimaryTextPaint);
        canvas.DrawCircle(textLeft + accentRadius, statusCenterY, accentRadius, _ViewerStatusAccentPaint);
        canvas.DrawText(status, textLeft + (accentRadius * 2f) + accentGap, statusBaseline, _ViewerStatusSecondaryTextPaint);
    }

    private void SetViewerStatusTextSizes(
        string title,
        string status,
        float maxBoxWidth,
        float horizontalPadding,
        float accentRadius,
        float accentGap,
        ref float titleTextSize,
        ref float statusTextSize)
    {
        const float MinimumTitleTextSize = 18f;
        const float MinimumStatusTextSize = 14f;

        while (titleTextSize > MinimumTitleTextSize || statusTextSize > MinimumStatusTextSize)
        {
            _ViewerStatusPrimaryTextPaint.TextSize = titleTextSize;
            _ViewerStatusSecondaryTextPaint.TextSize = statusTextSize;

            float titleWidth = _ViewerStatusPrimaryTextPaint.MeasureText(title);
            float statusWidth = _ViewerStatusSecondaryTextPaint.MeasureText(status);
            float statusRowWidth = (accentRadius * 2f) + accentGap + statusWidth;
            float requiredBoxWidth = MathF.Max(titleWidth, statusRowWidth) + (horizontalPadding * 2f);
            if (requiredBoxWidth <= maxBoxWidth)
            {
                return;
            }

            titleTextSize = MathF.Max(MinimumTitleTextSize, titleTextSize - 1f);
            statusTextSize = MathF.Max(MinimumStatusTextSize, statusTextSize - 1f);
        }

        _ViewerStatusPrimaryTextPaint.TextSize = titleTextSize;
        _ViewerStatusSecondaryTextPaint.TextSize = statusTextSize;
    }

    private static void DrawLine(
        Canvas canvas,
        AndroidGraphics.RectF viewport,
        AndroidGraphics.Paint paint,
        LineOverlay line)
    {
        (float x0, float y0) = ToCanvas(line.P0, viewport);
        (float x1, float y1) = ToCanvas(line.P1, viewport);
        canvas.DrawLine(x0, y0, x1, y1, paint);
    }

    private static void DrawPolygon(
        Canvas canvas,
        AndroidGraphics.RectF viewport,
        AndroidGraphics.Paint paint,
        params OverlayPoint[] points)
    {
        if (points.Length < 2)
        {
            return;
        }

        AndroidGraphics.Path path = new();
        (float firstX, float firstY) = ToCanvas(points[0], viewport);
        path.MoveTo(firstX, firstY);

        for (int i = 1; i < points.Length; i++)
        {
            (float x, float y) = ToCanvas(points[i], viewport);
            path.LineTo(x, y);
        }

        path.Close();
        canvas.DrawPath(path, paint);
    }

    private static AndroidGraphics.Color CreateColor(uint argb)
        => AndroidGraphics.Color.Argb(
            (int)((argb >> 24) & 0xFF),
            (int)((argb >> 16) & 0xFF),
            (int)((argb >> 8) & 0xFF),
            (int)(argb & 0xFF));

    private static AndroidGraphics.Color CreateColorWithAlpha(uint argb, int alpha)
        => AndroidGraphics.Color.Argb(
            alpha,
            (int)((argb >> 16) & 0xFF),
            (int)((argb >> 8) & 0xFF),
            (int)(argb & 0xFF));

    private static TrapeziumOverlay? CreatePossessionArea(
        TrackingOverlayState trackingState,
        TableOverlayState tableState)
    {
        if (tableState.Bars.Count == 0)
        {
            return null;
        }

        TableBarOverlay[] orderedBars = [.. tableState.Bars.OrderBy(GetBarSortX)];
        TableBarOverlay[] possessionBars = [.. orderedBars.Where(bar => IsPossessionAreaBar(bar.Type, trackingState))];
        if (possessionBars.Length == 0)
        {
            return null;
        }

        TableBarOverlay firstPossessionBar = possessionBars[0];
        TableBarOverlay lastPossessionBar = possessionBars[^1];
        int firstPossessionBarIndex = Array.IndexOf(orderedBars, firstPossessionBar);
        int lastPossessionBarIndex = Array.IndexOf(orderedBars, lastPossessionBar);
        if (firstPossessionBarIndex < 0 || lastPossessionBarIndex < 0)
        {
            return null;
        }

        TableBarOverlay? previousBar = firstPossessionBarIndex > 0
            ? orderedBars[firstPossessionBarIndex - 1]
            : null;
        TableBarOverlay? nextBar = lastPossessionBarIndex < orderedBars.Length - 1
            ? orderedBars[lastPossessionBarIndex + 1]
            : null;

        OverlayPoint upperLeft = previousBar is null
            ? tableState.Boundary.UpperLeft
            : GetMidpoint(previousBar.Center.P0, firstPossessionBar.Center.P0);
        OverlayPoint upperRight = nextBar is null
            ? tableState.Boundary.UpperRight
            : GetMidpoint(lastPossessionBar.Center.P0, nextBar.Center.P0);
        OverlayPoint lowerLeft = previousBar is null
            ? tableState.Boundary.LowerLeft
            : GetMidpoint(previousBar.Center.P1, firstPossessionBar.Center.P1);
        OverlayPoint lowerRight = nextBar is null
            ? tableState.Boundary.LowerRight
            : GetMidpoint(lastPossessionBar.Center.P1, nextBar.Center.P1);

        return new TrapeziumOverlay(upperLeft, upperRight, lowerLeft, lowerRight);
    }

    private static bool IsPossessionAreaBar(BarTypeMessage barType, TrackingOverlayState trackingState)
    {
        return (trackingState.PossessingTeam, trackingState.PossessionArea, barType) switch
        {
            (Team.A, PossessionArea.Defense, BarTypeMessage.A1) => true,
            (Team.A, PossessionArea.Defense, BarTypeMessage.A2) => true,
            (Team.B, PossessionArea.Defense, BarTypeMessage.B2) => true,
            (Team.B, PossessionArea.Defense, BarTypeMessage.B1) => true,
            (Team.A, PossessionArea.FiveBar, BarTypeMessage.A5) => true,
            (Team.B, PossessionArea.FiveBar, BarTypeMessage.B5) => true,
            (Team.A, PossessionArea.ThreeBar, BarTypeMessage.A3) => true,
            (Team.B, PossessionArea.ThreeBar, BarTypeMessage.B3) => true,
            _ => false,
        };
    }

    private static float GetBarSortX(TableBarOverlay bar)
        => (bar.Center.P0.X + bar.Center.P1.X) * 0.5f;

    private static OverlayPoint GetMidpoint(OverlayPoint first, OverlayPoint second)
        => new(
            (first.X + second.X) * 0.5f,
            (first.Y + second.Y) * 0.5f);

    private static TableBarOverlay? GetPossessionBar(
        TrackingOverlayState trackingState,
        TableOverlayState tableState)
    {
        if (trackingState.PossessingTeam == Team.None || trackingState.PossessionArea == PossessionArea.None)
        {
            return null;
        }

        return (trackingState.PossessingTeam, trackingState.PossessionArea) switch
        {
            (Team.A, PossessionArea.Defense) => tableState.Bars.FirstOrDefault(bar => bar.Type == BarTypeMessage.A2),
            (Team.B, PossessionArea.Defense) => tableState.Bars.FirstOrDefault(bar => bar.Type == BarTypeMessage.B2),
            (Team.A, PossessionArea.FiveBar) => tableState.Bars.FirstOrDefault(bar => bar.Type == BarTypeMessage.A5),
            (Team.B, PossessionArea.FiveBar) => tableState.Bars.FirstOrDefault(bar => bar.Type == BarTypeMessage.B5),
            (Team.A, PossessionArea.ThreeBar) => tableState.Bars.FirstOrDefault(bar => bar.Type == BarTypeMessage.A3),
            (Team.B, PossessionArea.ThreeBar) => tableState.Bars.FirstOrDefault(bar => bar.Type == BarTypeMessage.B3),
            _ => null,
        };
    }

    private static float GetBarTopX(TableBarOverlay bar)
    {
        return bar.Center.P0.X;
    }

    private static string FormatPossessionTime(int possessionTimeMs)
    {
        int seconds = Math.Max(0, possessionTimeMs) / 1000;
        int milliseconds = Math.Max(0, possessionTimeMs) % 1000;
        return $"{seconds}.{milliseconds:000}";
    }

    private static string FormatMetric(TrackingOverlayMetric metric)
    {
        return $"{metric.Name} {metric.Value:0.0} {metric.Unit}";
    }

    private static VideoDisplayMode GetDisplayMode(SessionUiState state)
    {
        if (!state.IsRunning || state.Mode != SessionMode.Game)
        {
            return VideoDisplayMode.None;
        }

        return state.IsReplayActive ? VideoDisplayMode.Replay : VideoDisplayMode.Live;
    }

    private static string GetDisplayModeText(VideoDisplayMode mode)
    {
        return mode == VideoDisplayMode.Replay ? "REPLAY" : "LIVE";
    }

    private static string GetViewerStatusText(SessionUiState state)
    {
        if (state.IsFaulted)
        {
            return "Recorder fault";
        }

        return state.IsConnected
            ? "Recorder connected"
            : "Connecting to Recorder";
    }

    private static string CreateViewerTitle()
    {
        string version = Microsoft.Maui.ApplicationModel.AppInfo.Current.VersionString;
        return string.IsNullOrWhiteSpace(version)
            ? "FoosVision Viewer"
            : $"FoosVision Viewer {version}";
    }

    private static AndroidGraphics.Color GetDisplayModeAccentColor(VideoDisplayMode mode)
    {
        return mode == VideoDisplayMode.Replay
            ? AndroidGraphics.Color.Argb(255, 255, 179, 71)
            : AndroidGraphics.Color.Argb(255, 234, 67, 53);
    }

    private static AndroidGraphics.Color GetViewerStatusAccentColor(SessionUiState state)
    {
        if (state.IsFaulted)
        {
            return AndroidGraphics.Color.Argb(255, 255, 112, 67);
        }

        return state.IsConnected
            ? AndroidGraphics.Color.Argb(255, 49, 208, 127)
            : AndroidGraphics.Color.Argb(255, 95, 104, 115);
    }

    private bool IsModeAccentVisible()
    {
        if (!IsModeBlinkActive())
        {
            return true;
        }

        long elapsedMs = SystemClock.UptimeMillis() - _ModeBlinkStartedMs;
        long phase = elapsedMs / _ModeBlinkIntervalMs;
        return phase % 2L == 1L;
    }

    private bool IsModeBlinkActive()
    {
        if (_ModeBlinkStartedMs == 0L)
        {
            return false;
        }

        long elapsedMs = SystemClock.UptimeMillis() - _ModeBlinkStartedMs;
        return elapsedMs < _ModeBlinkIntervalMs * _ModeBlinkToggles;
    }

    private static AndroidGraphics.Color GetCandidateColor(TrackingStatus status)
    {
        return status switch
        {
            TrackingStatus.Predicted => AndroidGraphics.Color.Argb(210, 255, 179, 71),
            _ => AndroidGraphics.Color.Argb(220, 255, 112, 67),
        };
    }

    private static float GetCandidateStrokeWidth(TrackingConfidence confidence, float viewportScale)
    {
        return confidence switch
        {
            TrackingConfidence.Average => ScaleOverlayStrokeWidth(5f, viewportScale),
            _ => ScaleOverlayStrokeWidth(3f, viewportScale),
        };
    }

    private static (AndroidGraphics.Color Color, float StrokeWidth) GetObservationStyle(
        ObservationQualityLevel qualityLevel,
        float viewportScale)
    {
        return qualityLevel switch
        {
            ObservationQualityLevel.VeryHighQuality => (AndroidGraphics.Color.Argb(240, 234, 67, 53), ScaleOverlayStrokeWidth(7f, viewportScale)),
            ObservationQualityLevel.HighQuality => (AndroidGraphics.Color.Argb(220, 234, 67, 53), ScaleOverlayStrokeWidth(5f, viewportScale)),
            ObservationQualityLevel.LowQuality => (AndroidGraphics.Color.Argb(170, 234, 67, 53), ScaleOverlayStrokeWidth(3f, viewportScale)),
            _ => (AndroidGraphics.Color.Argb(100, 234, 67, 53), ScaleOverlayStrokeWidth(1f, viewportScale)),
        };
    }

    private static float ScaleOverlayStrokeWidth(float strokeWidthPx, float viewportScale)
    {
        return MathF.Max(strokeWidthPx, viewportScale * strokeWidthPx / 1080f);
    }

    private static (float X, float Y) ToCanvas(OverlayPoint relativePoint, AndroidGraphics.RectF viewport)
    {
        return (
            viewport.Left + (relativePoint.X * viewport.Width()),
            viewport.Top + (relativePoint.Y * viewport.Height()));
    }

    private static void WriteBallDetectionMaskPixels(byte[] inputGray8, int pixelCount, int[] outputArgb)
    {
        for (int i = 0; i < pixelCount; i++)
        {
            outputArgb[i] = inputGray8[i] == 0 ? 0 : _BallDetectionMaskColor;
        }
    }

    private static AndroidGraphics.RectF CalculateVideoViewport(float availableWidth, float availableHeight)
    {
        float availableAspectRatio = availableWidth / availableHeight;

        if (availableAspectRatio >= _ReferenceAspectRatio)
        {
            float viewportWidth = availableHeight * _ReferenceAspectRatio;
            return new AndroidGraphics.RectF(0f, 0f, viewportWidth, availableHeight);
        }

        float viewportHeight = availableWidth / _ReferenceAspectRatio;
        return new AndroidGraphics.RectF(0f, 0f, availableWidth, viewportHeight);
    }

    private enum VideoDisplayMode
    {
        None,
        Live,
        Replay,
    }
}
