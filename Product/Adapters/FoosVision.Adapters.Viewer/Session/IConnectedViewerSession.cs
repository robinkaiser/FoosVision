// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Common;

namespace FoosVision.Adapters.Viewer.Session;

public interface IConnectedViewerSession : IDisposable
{
    RecorderConnection Connection { get; }

    IRecorderLiveDataSubscriber LiveDataSubscriber { get; }

    IRecorderLiveAnalysisSubscriber LiveAnalysisSubscriber { get; }

    void AttachRuntimeStateSink(IRecorderRuntimeStateSink sink);

    Task<CommandResponse> StartInstallAsync(Guid commandId, CancellationToken ct);

    Task<CommandResponse> StopInstallAsync(Guid commandId, CancellationToken ct);

    Task<CommandResponse> StartGameAsync(Guid commandId, CancellationToken ct);

    Task<CommandResponse> StopGameAsync(Guid commandId, CancellationToken ct);
}
