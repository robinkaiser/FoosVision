// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Adapters.Recorder.Diagnostics;

public interface IVideoDumpOrchestrator
{
    bool TryScheduleDump(VideoDumpSessionKind sessionKind);
}
