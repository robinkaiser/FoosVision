// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Content;
using Android.Views;

namespace FoosVision.Viewer.App.Platforms.Android.Screen.Page;

public class OrientationListener : OrientationEventListener
{
    private readonly Action<float> _ApplyRotation;
    private readonly Context _Context;
    private float _CurrentRotation;

    public OrientationListener(Context context, Action<float> applyRotation)
        : base(context)
    {
        _Context = context;
        _ApplyRotation = applyRotation;
    }

    public void ApplyInitialRotation()
    {
        float initialRotation = GetInitialRotation(_Context);
        _CurrentRotation = initialRotation;
        _ApplyRotation(initialRotation);
    }

    public override void OnOrientationChanged(int orientation)
    {
        if (orientation == OrientationUnknown)
        {
            return;
        }

        float targetRotation = GetTargetRotation(orientation);
        if (Math.Abs(targetRotation - _CurrentRotation) < 0.1f)
        {
            return;
        }

        _CurrentRotation = targetRotation;
        _ApplyRotation(targetRotation);
    }

    private static float GetTargetRotation(int orientation)
    {
        if (orientation is >= 315 or < 45)
        {
            return 270f;
        }

        if (orientation is >= 45 and < 135)
        {
            return 180f;
        }

        if (orientation is >= 135 and < 225)
        {
            return 90f;
        }

        if (orientation is >= 225 and < 315)
        {
            return 0f;
        }

        return 0f;
    }

    private static float GetInitialRotation(Context context)
    {
        Display? display = context.Display;
        if (display is null)
        {
            return 0f;
        }

        return display.Rotation switch
        {
            SurfaceOrientation.Rotation0 => 270f,
            SurfaceOrientation.Rotation90 => 180f,
            SurfaceOrientation.Rotation180 => 90f,
            SurfaceOrientation.Rotation270 => 0f,
            _ => 270f,
        };
    }
}
