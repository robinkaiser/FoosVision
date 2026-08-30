// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Adapters.Recorder.Diagnostics;

public interface IVideoDumpBackgroundQueue
{
    void Enqueue(Func<CancellationToken, Task> work);
}
