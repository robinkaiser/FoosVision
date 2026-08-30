// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Protocol.Connectivity.Abstractions;

/// <summary>
/// Viewer-side port: subscribes to recorder live data events (PUB/SUB).
/// </summary>
public interface IRecorderLiveDataSubscriber : IDisposable
{
    IDisposable Subscribe<TMessage>(Action<TMessage> onMessage);
}
