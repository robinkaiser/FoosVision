// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Protocol.Connectivity.Abstractions;

/// <summary>
/// Viewer-side port: subscribes to recorder live analysis messages (PUB/SUB).
/// </summary>
public interface IRecorderLiveAnalysisSubscriber : IDisposable
{
    IDisposable Subscribe<TMessage>(Action<TMessage> onMessage);
}
