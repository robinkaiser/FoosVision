// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.Commands;
using FoosVision.UseCases.Setup.StartSetup;
using FoosVision.UseCases.Setup.StopSetup;

namespace FoosVision.Adapters.Recorder.Setup.Control;

public class SetupCommandHandler
{
    private readonly IStartSetupInputPort _Start;
    private readonly IStopSetupInputPort _Stop;
    private readonly Func<Guid, IStartSetupOutputPort> _CreateStartPresenter;
    private readonly Func<Guid, IStopSetupOutputPort> _CreateStopPresenter;

    public SetupCommandHandler(
        IStartSetupInputPort start,
        IStopSetupInputPort stop,
        Func<Guid, IStartSetupOutputPort> createStartPresenter,
        Func<Guid, IStopSetupOutputPort> createStopPresenter)
    {
        _Start = start;
        _Stop = stop;
        _CreateStartPresenter = createStartPresenter;
        _CreateStopPresenter = createStopPresenter;
    }

    public async Task Handle(StartSetupCommand cmd, CancellationToken ct)
    {
        var request = new StartSetupRequest();
        var output = _CreateStartPresenter(cmd.CommandId);

        await _Start.Handle(request, output, ct);
    }

    public async Task Handle(StopSetupCommand cmd, CancellationToken ct)
    {
        var request = new StopSetupRequest();
        var output = _CreateStopPresenter(cmd.CommandId);

        await _Stop.Handle(request, output, ct);
    }
}
