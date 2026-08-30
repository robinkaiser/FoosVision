// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Viewer.App.Screen.Stage;

public class StageView : View
{
    public Func<IViewerScreenRuntime, Task>? RuntimeAttachedAsync { private get; set; }

    internal Task AttachRuntimeAsync(IViewerScreenRuntime runtime)
    {
        return RuntimeAttachedAsync?.Invoke(runtime) ?? Task.CompletedTask;
    }
}
