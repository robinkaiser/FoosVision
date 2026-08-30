// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Protocol.Connectivity.Abstractions;

namespace FoosVision.Viewer.Composition.Modules;

public class RuntimeStateModule
{
    private readonly IRecorderEventSubscriber _EventSubscriber;
    private bool _SinkAttached;

    public RuntimeStateModule(IRecorderEventSubscriber eventSubscriber)
    {
        _EventSubscriber = eventSubscriber;
    }

    public void Attach(IRecorderRuntimeStateSink sink)
    {
        if (_SinkAttached)
        {
            throw new InvalidOperationException("Runtime state sink is already attached.");
        }

        _ = new RecorderRuntimeStatePresenter(_EventSubscriber, sink);
        _SinkAttached = true;
    }
}
