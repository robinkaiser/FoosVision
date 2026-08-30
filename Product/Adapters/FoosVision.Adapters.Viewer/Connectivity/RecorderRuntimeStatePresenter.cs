// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Events;

namespace FoosVision.Adapters.Viewer.Connectivity;

public interface IRecorderRuntimeStateSink
{
    void OnRecorderRuntimeStateChanged(RecorderRuntimeStateChanged state);
}

public class RecorderRuntimeStatePresenter
{
    public RecorderRuntimeStatePresenter(
        IRecorderEventSubscriber subscriber,
        IRecorderRuntimeStateSink sink)
    {
        subscriber.Subscribe<RecorderRuntimeStateChanged>(sink.OnRecorderRuntimeStateChanged);
    }
}
