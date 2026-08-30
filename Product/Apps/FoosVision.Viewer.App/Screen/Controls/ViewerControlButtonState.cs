// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Viewer.App.Screen.Controls;

public record ViewerControlButtonState(
    string Text,
    bool IsEnabled,
    Color BackgroundColor,
    Color TextColor,
    Color BorderColor);
