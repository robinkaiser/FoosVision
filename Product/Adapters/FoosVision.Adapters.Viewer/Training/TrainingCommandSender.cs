// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Commands;
using FoosVision.Protocol.Messages.Common;

namespace FoosVision.Adapters.Viewer.Training;

public class TrainingCommandSender
{
    private readonly IRecorderCommandClient _Gateway;

    public TrainingCommandSender(IRecorderCommandClient gateway)
    {
        _Gateway = gateway;
    }

    public Task<CommandResponse> Start(Guid commandId, CancellationToken ct)
    {
        StartTrainingCommand command = new()
        {
            CommandId = commandId,
        };

        return _Gateway.SendAsync(command, ct);
    }

    public Task<CommandResponse> Stop(Guid commandId, CancellationToken ct)
    {
        StopTrainingCommand command = new()
        {
            CommandId = commandId,
        };

        return _Gateway.SendAsync(command, ct);
    }
}
