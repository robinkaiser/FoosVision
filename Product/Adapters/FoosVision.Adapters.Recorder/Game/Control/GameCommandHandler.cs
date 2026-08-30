// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.Commands;
using FoosVision.UseCases.Game.StartGame;
using FoosVision.UseCases.Game.StopGame;

namespace FoosVision.Adapters.Recorder.Game.Control;

public class GameCommandHandler
{
    private readonly IStartGameInputPort _Start;
    private readonly IStopGameInputPort _Stop;
    private readonly Func<Guid, IStartGameOutputPort> _CreateStartPresenter;
    private readonly Func<Guid, IStopGameOutputPort> _CreateStopPresenter;

    public GameCommandHandler(
        IStartGameInputPort start,
        IStopGameInputPort stop,
        Func<Guid, IStartGameOutputPort> createStartPresenter,
        Func<Guid, IStopGameOutputPort> createStopPresenter)
    {
        _Start = start;
        _Stop = stop;
        _CreateStartPresenter = createStartPresenter;
        _CreateStopPresenter = createStopPresenter;
    }

    public async Task Handle(StartGameCommand cmd, CancellationToken ct)
    {
        var request = new StartGameRequest();
        var output = _CreateStartPresenter(cmd.CommandId);

        await _Start.Handle(request, output, ct);
    }

    public async Task Handle(StopGameCommand cmd, CancellationToken ct)
    {
        var request = new StopGameRequest();
        var output = _CreateStopPresenter(cmd.CommandId);

        await _Stop.Handle(request, output, ct);
    }
}
