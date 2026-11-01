// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.Commands;
using FoosVision.UseCases.Training.StartTraining;
using FoosVision.UseCases.Training.StopTraining;

namespace FoosVision.Adapters.Recorder.Training.Control;

public class TrainingCommandHandler
{
    private readonly IStartTrainingInputPort _Start;
    private readonly IStopTrainingInputPort _Stop;
    private readonly Func<Guid, IStartTrainingOutputPort> _CreateStartPresenter;
    private readonly Func<Guid, IStopTrainingOutputPort> _CreateStopPresenter;

    public TrainingCommandHandler(
        IStartTrainingInputPort start,
        IStopTrainingInputPort stop,
        Func<Guid, IStartTrainingOutputPort> createStartPresenter,
        Func<Guid, IStopTrainingOutputPort> createStopPresenter)
    {
        _Start = start;
        _Stop = stop;
        _CreateStartPresenter = createStartPresenter;
        _CreateStopPresenter = createStopPresenter;
    }

    public async Task Handle(StartTrainingCommand cmd, CancellationToken ct)
    {
        var request = new StartTrainingRequest();
        var output = _CreateStartPresenter(cmd.CommandId);

        await _Start.Handle(request, output, ct);
    }

    public async Task Handle(StopTrainingCommand cmd, CancellationToken ct)
    {
        var request = new StopTrainingRequest();
        var output = _CreateStopPresenter(cmd.CommandId);

        await _Stop.Handle(request, output, ct);
    }
}
