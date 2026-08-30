// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Diagnostics;

namespace FoosVision.Recorder.Composition.Diagnostics;

public class VideoDumpThreadPoolBackgroundQueue : IVideoDumpBackgroundQueue
{
    public void Enqueue(Func<CancellationToken, Task> work)
    {
        _ = Task.Run(() => work(CancellationToken.None));
    }
}
