// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Widget;
using FoosVision.Viewer.App.Screen.Stage;
using Microsoft.Maui.Handlers;

namespace FoosVision.Viewer.App.Platforms.Android.Screen.Stage;

public class StageViewHandler : ViewHandler<StageView, FrameLayout>
{
    private static readonly IPropertyMapper<StageView, StageViewHandler> _StageViewMapper =
        new PropertyMapper<StageView, StageViewHandler>(ViewHandler.ViewMapper);

    public StageViewHandler()
        : base(_StageViewMapper)
    {
    }

    protected override FrameLayout CreatePlatformView()
    {
        return new StageLayout(Context);
    }

    protected override void ConnectHandler(FrameLayout platformView)
    {
        base.ConnectHandler(platformView);

        if (platformView is StageLayout host)
        {
            _ = VirtualView?.AttachRuntimeAsync(host);
        }
    }

    protected override void DisconnectHandler(FrameLayout platformView)
    {
        base.DisconnectHandler(platformView);
    }
}
