// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Views;
using AndroidX.Core.View;
using AndroidWindow = Android.Views.Window;

namespace FoosVision.Platforms.Android;

public static class AppWindowController
{
    public static void OnCreated(AndroidWindow? window)
    {
        if (window is null)
        {
            return;
        }

        window.AddFlags(WindowManagerFlags.KeepScreenOn);
        ApplyDisplayCutoutMode(window);
        ApplyImmersiveMode(window);
    }

    public static void OnResumed(AndroidWindow? window)
    {
        if (window is null)
        {
            return;
        }

        ApplyDisplayCutoutMode(window);
        ApplyImmersiveMode(window);
    }

    public static void OnWindowFocusChanged(AndroidWindow? window, bool hasFocus)
    {
        if (!hasFocus || window is null)
        {
            return;
        }

        ApplyDisplayCutoutMode(window);
        ApplyImmersiveMode(window);
    }

    private static void ApplyDisplayCutoutMode(AndroidWindow window)
    {
        WindowManagerLayoutParams attributes = window.Attributes!;
        attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
        window.Attributes = attributes;
    }

    private static void ApplyImmersiveMode(AndroidWindow window)
    {
        WindowCompat.SetDecorFitsSystemWindows(window, false);
        window.DecorView.WindowInsetsController?.Hide(WindowInsets.Type.StatusBars() | WindowInsets.Type.NavigationBars());
    }
}
