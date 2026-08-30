// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.Commands;
using FoosVision.UseCases.Installation.StartInstall;
using FoosVision.UseCases.Installation.StopInstall;

namespace FoosVision.Adapters.Recorder.Installation.Control;

public class InstallCommandHandler
{
    private readonly IStartInstallInputPort _Start;
    private readonly IStopInstallInputPort _Stop;
    private readonly Func<Guid, IStartInstallOutputPort> _CreateStartPresenter;
    private readonly Func<Guid, IStopInstallOutputPort> _CreateStopPresenter;

    public InstallCommandHandler(
        IStartInstallInputPort start,
        IStopInstallInputPort stop,
        Func<Guid, IStartInstallOutputPort> createStartPresenter,
        Func<Guid, IStopInstallOutputPort> createStopPresenter)
    {
        _Start = start;
        _Stop = stop;
        _CreateStartPresenter = createStartPresenter;
        _CreateStopPresenter = createStopPresenter;
    }

    public async Task Handle(StartInstallCommand cmd, CancellationToken ct)
    {
        var request = new StartInstallRequest();
        var output = _CreateStartPresenter(cmd.CommandId);

        await _Start.Handle(request, output, ct);
    }

    public async Task Handle(StopInstallCommand cmd, CancellationToken ct)
    {
        var request = new StopInstallRequest();
        var output = _CreateStopPresenter(cmd.CommandId);

        await _Stop.Handle(request, output, ct);
    }
}
