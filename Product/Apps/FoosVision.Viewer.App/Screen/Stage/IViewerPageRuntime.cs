// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Viewer.App.Screen.Stage;

public interface IViewerPageRuntime : IDisposable
{
    View StageContent { get; }

    void OnAppearing();

    void OnDisappearing();
}
