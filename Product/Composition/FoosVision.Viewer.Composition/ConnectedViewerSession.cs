// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Adapters.Viewer.Session;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Common;
using FoosVision.Viewer.Composition.Modules;

namespace FoosVision.Viewer.Composition;

public class ConnectedViewerSession :
    IConnectedViewerSession,
    IDisposable
{
    private readonly Action _Dispose;
    private int _Disposed;

    internal ConnectedViewerSession(
        RecorderConnection connection,
        InstallationModule installation,
        GameModule game,
        RuntimeStateModule runtimeState,
        IRecorderLiveDataSubscriber liveDataSubscriber,
        IRecorderLiveAnalysisSubscriber liveAnalysisSubscriber,
        Action dispose)
    {
        Connection = connection;
        Installation = installation;
        Game = game;
        RuntimeState = runtimeState;
        LiveDataSubscriber = liveDataSubscriber;
        LiveAnalysisSubscriber = liveAnalysisSubscriber;
        _Dispose = dispose;
    }

    public RecorderConnection Connection { get; }

    public InstallationModule Installation { get; }

    public GameModule Game { get; }

    public RuntimeStateModule RuntimeState { get; }

    public IRecorderLiveDataSubscriber LiveDataSubscriber { get; }

    public IRecorderLiveAnalysisSubscriber LiveAnalysisSubscriber { get; }

    public void AttachRuntimeStateSink(IRecorderRuntimeStateSink sink)
    {
        RuntimeState.Attach(sink);
    }

    public Task<CommandResponse> StartInstallAsync(Guid commandId, CancellationToken ct)
    {
        return Installation.CommandSender.Start(commandId, ct);
    }

    public Task<CommandResponse> StopInstallAsync(Guid commandId, CancellationToken ct)
    {
        return Installation.CommandSender.Stop(commandId, ct);
    }

    public Task<CommandResponse> StartGameAsync(Guid commandId, CancellationToken ct)
    {
        return Game.CommandSender.Start(commandId, ct);
    }

    public Task<CommandResponse> StopGameAsync(Guid commandId, CancellationToken ct)
    {
        return Game.CommandSender.Stop(commandId, ct);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Interlocked.Exchange(ref _Disposed, 1) != 0)
        {
            return;
        }

        _Dispose();
    }
}
