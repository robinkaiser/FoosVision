// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Protocol.Connectivity.Abstractions;

/// <summary>
/// Viewer-side port: subscribes to recorder events (PUB/SUB).
/// </summary>
public interface IRecorderEventSubscriber : IDisposable
{
    IDisposable Subscribe<TEvent>(Action<TEvent> onMessage);
}
