// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Ports.Media;

namespace FoosVision.Adapters.Viewer.Session.Replay;

public record ReplayFrame(IYuvFrameHandle Frame);
