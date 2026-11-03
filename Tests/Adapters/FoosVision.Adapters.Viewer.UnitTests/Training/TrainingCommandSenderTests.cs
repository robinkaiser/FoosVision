// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Training;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Commands;
using FoosVision.Protocol.Messages.Common;
using NSubstitute;

namespace FoosVision.Adapters.Viewer.UnitTests.Training;

public class TrainingCommandSenderTests
{
    [Fact]
    public async Task Start_and_stop_forward_command_id_token_and_response()
    {
        IRecorderCommandClient client = Substitute.For<IRecorderCommandClient>();
        TrainingCommandSender sut = new(client);
        Guid commandId = Guid.NewGuid();
        using CancellationTokenSource cts = new();
        CommandResponse response = new() { CommandId = commandId, Accepted = true, Error = string.Empty };
        client.SendAsync(Arg.Any<StartTrainingCommand>(), cts.Token).Returns(response);
        client.SendAsync(Arg.Any<StopTrainingCommand>(), cts.Token).Returns(response);

        Assert.Same(response, await sut.Start(commandId, cts.Token));
        Assert.Same(response, await sut.Stop(commandId, cts.Token));

        await client.Received(1).SendAsync(Arg.Is<StartTrainingCommand>(command => command.CommandId == commandId), cts.Token);
        await client.Received(1).SendAsync(Arg.Is<StopTrainingCommand>(command => command.CommandId == commandId), cts.Token);
    }
}
