// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.NetMq;
using FoosVision.Protocol.Connectivity.Configuration;
using FoosVision.Protocol.Messages.Events;
using NetMQ;
using RecorderCli;

Console.WriteLine("FoosVision Recorder Protocol Tester");

using var handshakeServer = new HandshakeServer();
handshakeServer.Start();

var lastCommandTracker = new LastCommandTracker();

var commandsBind = $"tcp://*:{DefaultPorts.CommandsReqRepTcp}";
var eventsBind = $"tcp://*:{DefaultPorts.EventsPubSubTcp}";

using var commandServer = new RecorderCommandServerHost(new RecorderCommandRouter(lastCommandTracker));
commandServer.Start(commandsBind);

using var eventPublisher = new RecorderEventPublisher();
eventPublisher.Bind(eventsBind);

Console.WriteLine($"Commands REP bind : {commandsBind}");
Console.WriteLine($"Events   PUB bind : {eventsBind}");
Console.WriteLine();

using var cts = new CancellationTokenSource();
var ctrlCCount = 0;

Console.CancelKeyPress += (_, e) =>
{
    ctrlCCount++;

    if (ctrlCCount == 1)
    {
        e.Cancel = true;
        cts.Cancel();
        Console.WriteLine("Stopping (Ctrl+C pressed).");
        return;
    }

    e.Cancel = false;
};

PrintEventMenu();

try
{
    while (!cts.IsCancellationRequested)
    {
        Console.Write("> ");
        var line = await ReadLineAsync(cts.Token);
        if (line is null) break;

        line = line.Trim();
        if (line.Length == 0) continue;

        if (line.Equals("help", StringComparison.OrdinalIgnoreCase) ||
            line.Equals("h", StringComparison.OrdinalIgnoreCase))
        {
            PrintEventMenu();
            continue;
        }

        if (!int.TryParse(line, out var choice))
        {
            Console.WriteLine("Invalid input. Enter a number or 'help'.");
            continue;
        }

        var cmdId = lastCommandTracker.GetOrNew();
        var sessionId = Guid.NewGuid();

        var actions = new Dictionary<int, Func<Task>>
        {
            [0] = () => Publish(new RecorderRuntimeStateChanged
            {
                Sequence = 1,
                Mode = RecorderRuntimeMode.Idle,
                ActiveSessionId = null,
                Reason = RecorderStateChangeReason.None,
                Detail = string.Empty,
            }),
            [1] = () => Publish(new RecorderRuntimeStateChanged
            {
                Sequence = 2,
                Mode = RecorderRuntimeMode.InstallRunning,
                ActiveSessionId = sessionId,
                Reason = RecorderStateChangeReason.CommandCompleted,
                Detail = string.Empty,
            }),
            [2] = () => Publish(new RecorderRuntimeStateChanged
            {
                Sequence = 3,
                Mode = RecorderRuntimeMode.GameRunning,
                ActiveSessionId = sessionId,
                Reason = RecorderStateChangeReason.CommandCompleted,
                Detail = string.Empty,
            }),
            [3] = () => Publish(new RecorderRuntimeStateChanged
            {
                Sequence = 4,
                Mode = RecorderRuntimeMode.Idle,
                ActiveSessionId = null,
                Reason = RecorderStateChangeReason.EndOfInput,
                Detail = "CLI end-of-input simulation",
            }),
            [4] = () => Publish(new RecorderRuntimeStateChanged
            {
                Sequence = 5,
                Mode = RecorderRuntimeMode.Faulted,
                ActiveSessionId = null,
                Reason = RecorderStateChangeReason.InternalError,
                Detail = $"CLI test failure for CommandId={cmdId}",
            }),
        };

        if (!actions.TryGetValue(choice, out var action))
        {
            Console.WriteLine("Unknown event number. Type 'help' to see the list.");
            continue;
        }

        await action();
    }
}
catch (OperationCanceledException)
{
    // expected
}
finally
{
    NetMQConfig.Cleanup();
}

Console.WriteLine("Recorder stopped.");

async Task Publish<TEvent>(TEvent evt)
{
    await eventPublisher.PublishAsync(evt, cts.Token);
    Console.WriteLine($"[EVT] Published {typeof(TEvent).Name}");
}

static void PrintEventMenu()
{
    Console.WriteLine("Publish events (enter number):");
    Console.WriteLine("  0  RuntimeState Idle");
    Console.WriteLine("  1  RuntimeState InstallRunning");
    Console.WriteLine("  2  RuntimeState GameRunning");
    Console.WriteLine("  3  RuntimeState Idle (EndOfInput)");
    Console.WriteLine("  4  RuntimeState Faulted");
    Console.WriteLine("Type 'help' to show this menu again.");
    Console.WriteLine();
}

static Task<string?> ReadLineAsync(CancellationToken ct)
    => Task.Run(() => Console.ReadLine(), ct);
