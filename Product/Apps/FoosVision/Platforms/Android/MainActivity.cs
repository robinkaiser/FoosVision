// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace FoosVision.Platforms.Android;

[Activity(
    Theme = "@style/Maui.MainTheme.NoActionBar",
    Exported = false,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const string _RoleExtraName = "org.foosvision.app.ROLE";

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        AppWindowController.OnWindowFocusChanged(Window, hasFocus);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        AppRole role = GetRole(Intent);
        AppLaunch.SetRole(role);
        ApplyRoleOrientation(role);

        base.OnCreate(null);
        AppWindowController.OnCreated(Window);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        if (intent is null)
        {
            return;
        }

        AppRole role = GetRole(intent);
        ApplyRoleOrientation(role);
        AppLaunch.RequestRole(role);
    }

    protected override void OnResume()
    {
        base.OnResume();
        AppWindowController.OnResumed(Window);
    }

    public static void PutRole(Intent intent, AppRole role)
    {
        intent.PutExtra(_RoleExtraName, role.ToString());
    }

    private static AppRole GetRole(Intent? intent)
    {
        string? role = intent?.GetStringExtra(_RoleExtraName);

        return role switch
        {
            nameof(AppRole.Recorder) => AppRole.Recorder,
            nameof(AppRole.Viewer) => AppRole.Viewer,
            _ => throw new InvalidOperationException($"Unsupported app role '{role}'."),
        };
    }

    private void ApplyRoleOrientation(AppRole role)
    {
        RequestedOrientation = role switch
        {
            AppRole.Recorder => ScreenOrientation.Unspecified,
            AppRole.Viewer => ScreenOrientation.Landscape,
            _ => throw new InvalidOperationException($"Unsupported app role '{role}'."),
        };
    }
}

public abstract class RoleLauncherActivity : Activity
{
    protected abstract AppRole Role { get; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        Intent intent = new(this, typeof(MainActivity));
        MainActivity.PutRole(intent, Role);
        intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        StartActivity(intent);
        Finish();
    }
}

[Activity(
    Name = "org.foosvision.app.ViewerLauncherActivity",
    Label = "Viewer",
    Icon = "@mipmap/appicon",
    RoundIcon = "@mipmap/appicon_round",
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    NoHistory = true,
    ExcludeFromRecents = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class ViewerLauncherActivity : RoleLauncherActivity
{
    protected override AppRole Role => AppRole.Viewer;
}

[Activity(
    Name = "org.foosvision.app.RecorderLauncherActivity",
    Label = "Recorder",
    Icon = "@mipmap/appicon_recorder",
    RoundIcon = "@mipmap/appicon_recorder_round",
    Theme = "@style/FoosVision.RecorderSplashTheme",
    MainLauncher = true,
    NoHistory = true,
    ExcludeFromRecents = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class RecorderLauncherActivity : RoleLauncherActivity
{
    protected override AppRole Role => AppRole.Recorder;
}
