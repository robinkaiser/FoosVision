// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Recorder.App;
using FoosVision.Recorder.App.Runtime;
using FoosVision.Viewer.App;
using FoosVision.Viewer.App.Runtime;

namespace FoosVision;

public partial class App : Application
{
    private AppRole _ActiveRole = AppRole.Unknown;
    private bool _IsSwitchingRole;
    private Window? _Window;

    public App()
    {
        InitializeComponent();
        AppLaunch.RoleRequested += OnRoleRequested;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _ActiveRole = AppLaunch.CurrentRole;
        _Window = new Window(CreatePage(_ActiveRole));

        return _Window;
    }

    protected override void CleanUp()
    {
        AppLaunch.RoleRequested -= OnRoleRequested;
        ShutdownRole(_ActiveRole);
        base.CleanUp();
    }

    private async void OnRoleRequested(AppRole role)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => OnRoleRequested(role));
            return;
        }

        await SwitchToRoleAsync(role);
    }

    private async Task SwitchToRoleAsync(AppRole role)
    {
        if (_Window is null || _ActiveRole == role || _IsSwitchingRole)
        {
            return;
        }

        _IsSwitchingRole = true;

        try
        {
            if (_Window.Page is { } page)
            {
                await StopRolePageAsync(page);
            }

            _Window.Page = new ContentPage();
            ShutdownRole(_ActiveRole);
            _ActiveRole = role;
            _Window.Page = CreatePage(role);
        }
        finally
        {
            _IsSwitchingRole = false;
        }
    }

    private static Page CreatePage(AppRole role)
    {
        return role switch
        {
            AppRole.Recorder => CreateRecorderPage(),
            AppRole.Viewer => CreateViewerPage(),
            _ => throw new InvalidOperationException($"Unsupported app role '{role}'."),
        };
    }

    private static Recorder.App.MainPage CreateRecorderPage()
    {
        RecorderLoggingBootstrap.Initialize(
            AppRolePaths.GetPreferredRecorderAppFilesPath(),
            AppRolePaths.GetRecorderAppDataPath());

        return RecorderAppComposition.CreateRecorderPage();
    }

    private static Viewer.App.MainPage CreateViewerPage()
    {
        ViewerLoggingBootstrap.Initialize(
            AppRolePaths.GetPreferredViewerAppFilesPath(),
            AppRolePaths.GetViewerAppDataPath());

        return ViewerAppComposition.CreateViewerPage();
    }

    private static async Task StopRolePageAsync(Page page)
    {
        switch (page)
        {
            case Recorder.App.MainPage recorderPage:
                await recorderPage.StopAsync(CancellationToken.None);
                break;
            case Viewer.App.MainPage viewerPage:
                viewerPage.Stop();
                break;
        }
    }

    private static void ShutdownRole(AppRole role)
    {
        switch (role)
        {
            case AppRole.Recorder:
                RecorderLoggingBootstrap.Shutdown();
                break;
            case AppRole.Viewer:
                ViewerLoggingBootstrap.Shutdown();
                break;
            case AppRole.Unknown:
                break;
            default:
                throw new InvalidOperationException($"Unsupported app role '{role}'.");
        }
    }
}
